using System.Xml.Linq;

namespace DotnetVersion.Services;

/// <summary>
/// Resolves centrally-managed NuGet package versions declared in a <c>Directory.Packages.props</c>
/// file (NuGet Central Package Management, CPM).
/// </summary>
public sealed class DirectoryPackagesPropsResolver
{
    private const string FileName = "Directory.Packages.props";

    /// <summary>
    /// Walks up the directory tree starting at <paramref name="startDirectory"/> looking for the
    /// nearest <c>Directory.Packages.props</c> file (mirroring MSBuild's own lookup), parses it,
    /// and returns a package id → version map. Returns an empty map when no such file is found
    /// or it cannot be parsed.
    /// </summary>
    public IReadOnlyDictionary<string, string> Resolve(string startDirectory)
    {
        var path = FindFile(startDirectory);
        return path is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : ParseContent(File.ReadAllText(path));
    }

    /// <summary>
    /// Parses the given <c>Directory.Packages.props</c> XML <paramref name="content"/> and
    /// returns a package id → version map built from <c>&lt;PackageVersion&gt;</c> elements.
    /// Returns an empty map when the content cannot be parsed as XML.
    /// </summary>
    public IReadOnlyDictionary<string, string> ParseContent(string content)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        XDocument doc;
        try
        {
            doc = XDocument.Parse(content);
        }
        catch
        {
            return map;
        }

        foreach (var element in doc.Descendants()
                     .Where(e => string.Equals(e.Name.LocalName, "PackageVersion", StringComparison.OrdinalIgnoreCase)))
        {
            var id = element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value;
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var version = element.Attribute("Version")?.Value
                ?? element.Elements().FirstOrDefault(c => string.Equals(c.Name.LocalName, "Version", StringComparison.OrdinalIgnoreCase))?.Value;

            if (!string.IsNullOrWhiteSpace(version))
                map[id.Trim()] = version.Trim();
        }

        return map;
    }

    /// <summary>
    /// Searches <paramref name="startDirectory"/> and each of its ancestors for a
    /// <c>Directory.Packages.props</c> file, returning the path of the nearest one found,
    /// or <see langword="null"/> if none exists.
    /// </summary>
    public static string? FindFile(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, FileName);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        return null;
    }
}
