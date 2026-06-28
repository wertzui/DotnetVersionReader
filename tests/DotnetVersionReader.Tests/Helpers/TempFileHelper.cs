namespace DotnetVersion.Tests.Helpers;

/// <summary>
/// Creates temporary .csproj files on disk and cleans them up after each test.
/// </summary>
public sealed class TempFileHelper : IDisposable
{
    private readonly List<string> _files       = [];
    private readonly List<string> _directories = [];

    /// <summary>
    /// Creates a temporary directory that has its own unique subdirectory per project,
    /// so that each project's "owned files" are isolated (important for dependency-graph tests).
    /// Returns the root directory and a mapping of project name → (csprojPath, projectDir).
    /// </summary>
    public (string RootDir, IReadOnlyDictionary<string, (string CsprojPath, string ProjectDir)> Projects)
        CreateProjectTree(IEnumerable<(string Name, string Content)> projects)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        _directories.Add(root);

        var map = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, content) in projects)
        {
            var projectDir = Path.Combine(root, name);
            Directory.CreateDirectory(projectDir);
            var csprojPath = Path.Combine(projectDir, $"{name}.csproj");
            File.WriteAllText(csprojPath, content);
            map[name] = (csprojPath, projectDir);
        }

        return (root, map);
    }

    /// <summary>
    /// Creates a plain file (not a .csproj) inside <paramref name="directory"/> with the given
    /// relative <paramref name="relativePath"/> and <paramref name="content"/>.
    /// </summary>
    public string CreateFile(string directory, string relativePath, string content = "")
    {
        var fullPath = Path.Combine(directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        _files.Add(fullPath);
        return fullPath;
    }

    /// <summary>Writes <paramref name="content"/> to a temporary .csproj file and returns its path.</summary>
    public string CreateCsproj(string content, string? projectName = null)
    {
        var fileName = $"{projectName ?? Guid.NewGuid().ToString("N")}.csproj";
        var path = Path.Combine(Path.GetTempPath(), fileName);
        File.WriteAllText(path, content);
        _files.Add(path);
        return path;
    }

    /// <summary>
    /// Creates a temporary directory, writes several .csproj files into it, and returns
    /// both the directory path and the individual file paths.
    /// </summary>
    public (string Directory, IReadOnlyList<string> Files) CreateDirectory(
        IEnumerable<(string Name, string Content)> projects)
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _directories.Add(dir);

        var files = new List<string>();
        foreach (var (name, content) in projects)
        {
            var path = Path.Combine(dir, $"{name}.csproj");
            File.WriteAllText(path, content);
            files.Add(path);
        }

        return (dir, files);
    }

    /// <summary>Writes a plain-text .sln file that references the given .csproj paths.</summary>
    public string CreateSln(IEnumerable<string> csprojPaths)
    {
        var slnPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sln");
        var lines = new List<string>
        {
            "",
            "Microsoft Visual Studio Solution File, Format Version 12.00",
            "# Visual Studio Version 17"
        };

        var guidCounter = 1;
        foreach (var csproj in csprojPaths)
        {
            var name     = Path.GetFileNameWithoutExtension(csproj);
            var relative = Path.GetRelativePath(Path.GetDirectoryName(slnPath)!, csproj);
            var projGuid = new Guid(guidCounter++, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0).ToString("B").ToUpperInvariant();
            lines.Add($"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{name}\", \"{relative}\", \"{projGuid}\"");
            lines.Add("EndProject");
        }

        File.WriteAllLines(slnPath, lines);
        _files.Add(slnPath);
        return slnPath;
    }

    /// <summary>Writes a .slnx file (XML) that references the given .csproj paths.</summary>
    public string CreateSlnx(IEnumerable<string> csprojPaths, string? baseDir = null)
    {
        var slnxPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.slnx");
        var dir = baseDir ?? Path.GetDirectoryName(slnxPath)!;

        var projectElements = csprojPaths
            .Select(p => $"  <Project Path=\"{Path.GetRelativePath(dir, p)}\" />")
            .ToList();

        var content = $"""
            <Solution>
            {string.Join(Environment.NewLine, projectElements)}
            </Solution>
            """;

        File.WriteAllText(slnxPath, content);
        _files.Add(slnxPath);
        return slnxPath;
    }

    public void Dispose()
    {
        foreach (var f in _files)
            try { File.Delete(f); } catch { /* best-effort */ }

        foreach (var d in _directories)
            try { Directory.Delete(d, recursive: true); } catch { /* best-effort */ }
    }
}

