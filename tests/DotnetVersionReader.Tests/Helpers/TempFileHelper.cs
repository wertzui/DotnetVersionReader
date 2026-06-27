namespace DotnetVersion.Tests.Helpers;

/// <summary>
/// Creates temporary .csproj files on disk and cleans them up after each test.
/// </summary>
public sealed class TempFileHelper : IDisposable
{
    private readonly List<string> _files = [];
    private readonly List<string> _directories = [];

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

