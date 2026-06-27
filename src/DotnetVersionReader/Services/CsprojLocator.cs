using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DotnetVersion.Services;

/// <summary>
/// Resolves a list of .csproj file paths from a given input
/// (a .csproj file, a .sln file, a .slnx file, a directory, or nothing / current directory).
/// </summary>
public sealed class CsprojLocator
{
    /// <summary>
    /// Returns all .csproj paths that match the given <paramref name="input"/>.
    /// </summary>
    /// <param name="input">
    /// Path to a .csproj, .sln, .slnx, a directory, or <see langword="null"/>
    /// to use the current directory.
    /// </param>
    public IReadOnlyList<string> Locate(string? input)
    {
        var resolved = string.IsNullOrWhiteSpace(input)
            ? Directory.GetCurrentDirectory()
            : input.Trim();

        if (File.Exists(resolved))
        {
            var ext = Path.GetExtension(resolved).ToLowerInvariant();

            return ext switch
            {
                ".csproj" => [resolved],
                ".sln"    => ReadFromSln(resolved),
                ".slnx"   => ReadFromSlnx(resolved),
                _         => throw new ArgumentException($"Unsupported file type: {ext}", nameof(input))
            };
        }

        if (Directory.Exists(resolved))
            return Directory.GetFiles(resolved, "*.csproj", SearchOption.AllDirectories);

        throw new ArgumentException($"Input not found: {resolved}", nameof(input));
    }

    // -------------------------------------------------------------------------
    // .sln  – classic text-based solution file
    // -------------------------------------------------------------------------
    private static IReadOnlyList<string> ReadFromSln(string slnPath)
    {
        var dir = Path.GetDirectoryName(slnPath)!;
        var results = new List<string>();

        // Project("{type-guid}") = "Name", "relative\path.csproj", "{project-guid}"
        var projectLineRegex = new Regex(
            @"Project\(""\{[^}]+\}""\)\s*=\s*""[^""]+""\s*,\s*""([^""]+\.csproj)""",
            RegexOptions.IgnoreCase);

        foreach (var line in File.ReadLines(slnPath))
        {
            var match = projectLineRegex.Match(line);
            if (!match.Success)
                continue;

            // Normalise directory separators so both Windows-style (\) and Unix-style (/) work.
            var rawRelative = match.Groups[1].Value
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            var fullPath = Path.GetFullPath(Path.Combine(dir, rawRelative));
            if (File.Exists(fullPath))
                results.Add(fullPath);
        }

        return results;
    }

    // -------------------------------------------------------------------------
    // .slnx – XML-based solution file (introduced in VS 2022 17.10)
    // -------------------------------------------------------------------------
    private static IReadOnlyList<string> ReadFromSlnx(string slnxPath)
    {
        var dir = Path.GetDirectoryName(slnxPath)!;
        var doc = XDocument.Load(slnxPath);

        return doc.Descendants("Project")
            .Select(e => e.Attribute("Path")?.Value)
            .Where(p => p is not null && p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(p => Path.GetFullPath(Path.Combine(dir, p!.Replace('\\', Path.DirectorySeparatorChar))))
            .Where(File.Exists)
            .ToList();
    }
}

