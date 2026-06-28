using System.Xml.Linq;

namespace DotnetVersion.Services;

/// <summary>
/// Builds an in-memory dependency graph from a set of .csproj files and uses it to determine
/// which projects are "affected" by a set of changed file paths.
/// </summary>
public sealed class DependencyGraphService
{
    // -------------------------------------------------------------------------
    // Public types
    // -------------------------------------------------------------------------

    /// <summary>
    /// A single node in the project dependency graph.
    /// </summary>
    public sealed class ProjectNode
    {
        /// <summary>Absolute path to the .csproj file.</summary>
        public required string CsprojPath { get; init; }

        /// <summary>The directory that contains the .csproj file.</summary>
        public required string ProjectDirectory { get; init; }

        /// <summary>
        /// Absolute paths of all files that "belong" to this project.
        /// Populated lazily on first use.
        /// </summary>
        public IReadOnlyList<string> OwnedFiles { get; internal set; } = [];

        /// <summary>
        /// Absolute paths of the .csproj files directly referenced by this project
        /// via <c>&lt;ProjectReference&gt;</c> elements.
        /// </summary>
        public IReadOnlyList<string> DirectProjectReferences { get; internal set; } = [];
    }

    /// <summary>
    /// A fully-resolved dependency graph for a set of projects.
    /// </summary>
    public sealed class DependencyGraph
    {
        /// <summary>All nodes keyed by their absolute .csproj path (normalized).</summary>
        public IReadOnlyDictionary<string, ProjectNode> Nodes { get; init; } = new Dictionary<string, ProjectNode>();

        /// <summary>
        /// Reverse dependency map: for each .csproj path, the set of .csproj paths that reference it.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> ReverseDependencies { get; init; }
            = new Dictionary<string, IReadOnlyList<string>>();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a <see cref="DependencyGraph"/> from the given collection of absolute .csproj paths.
    /// </summary>
    public DependencyGraph Build(IReadOnlyList<string> csprojPaths)
    {
        // Normalise all input paths once
        var normalised = csprojPaths
            .Select(NormalizePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var nodes = new Dictionary<string, ProjectNode>(StringComparer.OrdinalIgnoreCase);

        // First pass – create nodes and resolve direct project references (may discover
        // projects that are referenced but not in the original input list)
        var toVisit = new Queue<string>(normalised);
        while (toVisit.Count > 0)
        {
            var path = toVisit.Dequeue();
            if (nodes.ContainsKey(path))
                continue;

            var node = CreateNode(path);
            nodes[path] = node;

            // Queue newly discovered transitive references
            foreach (var refPath in node.DirectProjectReferences)
            {
                if (!nodes.ContainsKey(refPath))
                    toVisit.Enqueue(refPath);
            }
        }

        // Second pass – populate OwnedFiles for each node
        foreach (var node in nodes.Values)
            node.OwnedFiles = EnumerateOwnedFiles(node);

        // Build reverse-dependency index
        var reverse = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes.Values)
        {
            foreach (var refPath in node.DirectProjectReferences)
            {
                if (!reverse.TryGetValue(refPath, out var list))
                {
                    list = [];
                    reverse[refPath] = list;
                }
                list.Add(node.CsprojPath);
            }
        }

        var reverseFinal = reverse.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value,
            StringComparer.OrdinalIgnoreCase);

        return new DependencyGraph
        {
            Nodes = nodes,
            ReverseDependencies = reverseFinal
        };
    }

    /// <summary>
    /// Returns the subset of <paramref name="graph"/> nodes that are transitively affected by the
    /// given <paramref name="changedFiles"/>.
    ///
    /// A project is affected when:
    /// <list type="bullet">
    ///   <item>One of its <see cref="ProjectNode.OwnedFiles"/> is in <paramref name="changedFiles"/>, or</item>
    ///   <item>Its .csproj file itself is in <paramref name="changedFiles"/>, or</item>
    ///   <item>A project it depends on (transitively) is affected.</item>
    /// </list>
    /// </summary>
    public IReadOnlyList<ProjectNode> GetAffectedProjects(
        IReadOnlyList<string> changedFiles,
        DependencyGraph graph)
    {
        var changedSet = changedFiles
            .Select(NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Seed: projects directly touched by a changed file
        var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        foreach (var node in graph.Nodes.Values)
        {
            if (IsTouched(node, changedSet))
            {
                affected.Add(node.CsprojPath);
                queue.Enqueue(node.CsprojPath);
            }
        }

        // BFS up the reverse-dependency tree
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!graph.ReverseDependencies.TryGetValue(current, out var dependents))
                continue;

            foreach (var dependent in dependents)
            {
                if (affected.Add(dependent))
                    queue.Enqueue(dependent);
            }
        }

        return affected
            .Select(p => graph.Nodes[p])
            .OrderBy(n => n.CsprojPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static ProjectNode CreateNode(string csprojPath)
    {
        var dir = Path.GetDirectoryName(csprojPath) ?? string.Empty;

        IReadOnlyList<string> refs;
        try
        {
            refs = ParseProjectReferences(csprojPath, dir);
        }
        catch
        {
            refs = [];
        }

        return new ProjectNode
        {
            CsprojPath         = csprojPath,
            ProjectDirectory   = dir,
            DirectProjectReferences = refs
        };
    }

    private static IReadOnlyList<string> ParseProjectReferences(string csprojPath, string baseDir)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(csprojPath);
        }
        catch
        {
            return [];
        }

        return doc.Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "ProjectReference", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => NormalizePath(Path.GetFullPath(
                Path.Combine(baseDir, v!.Replace('\\', Path.DirectorySeparatorChar)))))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Enumerates all files in the project's directory tree that are considered "owned"
    /// by this project. This mirrors the SDK's implicit glob behaviour (all files under
    /// the project directory, excluding bin/ and obj/).
    /// </summary>
    private static IReadOnlyList<string> EnumerateOwnedFiles(ProjectNode node)
    {
        var dir = node.ProjectDirectory;
        if (!Directory.Exists(dir))
            return [node.CsprojPath];

        try
        {
            return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Where(f => !IsInBinOrObj(f, dir))
                .Select(NormalizePath)
                .ToList();
        }
        catch
        {
            // If we can't enumerate (permissions, etc.), fall back to just the .csproj
            return [NormalizePath(node.CsprojPath)];
        }
    }

    private static bool IsTouched(ProjectNode node, HashSet<string> changedSet)
    {
        // The .csproj itself changed
        if (changedSet.Contains(node.CsprojPath))
            return true;

        // Any changed file lives under this project's directory (but not in bin/ or obj/)
        var dir = node.ProjectDirectory;
        foreach (var file in changedSet)
        {
            if (IsUnderDirectory(file, dir) && !IsInBinOrObj(file, dir))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="filePath"/> is located inside
    /// <paramref name="directory"/> (including any depth of subdirectory).
    /// </summary>
    private static bool IsUnderDirectory(string filePath, string directory)
    {
        // Ensure both paths end with a separator so "PrefixX" doesn't match "Prefix" directories
        var dir  = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                 + Path.DirectorySeparatorChar;
        var file = filePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return file.StartsWith(dir, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInBinOrObj(string filePath, string projectDir)
    {
        var relative = Path.GetRelativePath(projectDir, filePath);
        // Treat paths that start with "bin" or "obj" as excluded
        var firstSegment = relative.Split(Path.DirectorySeparatorChar, 2)[0];
        return string.Equals(firstSegment, "bin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstSegment, "obj", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizePath(string path)
        => Path.GetFullPath(path);
}
