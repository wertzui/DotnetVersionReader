using System.Text.RegularExpressions;
using System.Xml.Linq;
using DotnetVersion.Models;

namespace DotnetVersion.Services;

/// <summary>
/// Parses a .csproj XML file into a <see cref="ProjectVersionInfo"/>.
/// </summary>
public sealed class CsprojParser
{
    private readonly DirectoryPackagesPropsResolver _cpmResolver = new();

    /// <summary>
    /// Parses the given .csproj file and returns its version information,
    /// or <see langword="null"/> when the file cannot be loaded.
    /// &lt;PackageReference&gt; versions that are not declared inline are resolved against
    /// the nearest <c>Directory.Packages.props</c> file found by walking up from the project's
    /// directory (NuGet Central Package Management).
    /// </summary>
    public ProjectVersionInfo? Parse(string csprojPath)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(csprojPath);
        }
        catch
        {
            return null;
        }

        var projectDir = Path.GetDirectoryName(csprojPath) ?? Directory.GetCurrentDirectory();
        var centralVersions = _cpmResolver.Resolve(projectDir);

        return BuildInfo(doc, csprojPath, centralVersions);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the document contains at least one element
    /// named <paramref name="elementName"/> whose text content matches <paramref name="pattern"/>.
    /// The search is case-insensitive for the element name and uses the provided regex for the value.
    /// </summary>
    public bool MatchesFilter(XDocument doc, string elementName, Regex pattern)
    {
        return doc.Descendants()
            .Any(e => string.Equals(e.Name.LocalName, elementName, StringComparison.OrdinalIgnoreCase)
                      && pattern.IsMatch(e.Value));
    }

    /// <summary>
    /// Loads the document for the given path and applies all filters.
    /// Returns <see langword="null"/> if the file cannot be loaded or any filter does not match.
    /// </summary>
    public ProjectVersionInfo? ParseWithFilters(string csprojPath, IReadOnlyList<(string Element, Regex Pattern)> filters)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(csprojPath);
        }
        catch
        {
            return null;
        }

        foreach (var (element, pattern) in filters)
        {
            if (!MatchesFilter(doc, element, pattern))
                return null;
        }

        var projectDir = Path.GetDirectoryName(csprojPath) ?? Directory.GetCurrentDirectory();
        var centralVersions = _cpmResolver.Resolve(projectDir);

        return BuildInfo(doc, csprojPath, centralVersions);
    }

    /// <summary>
    /// Parses .csproj XML from an in-memory <paramref name="content"/> string
    /// (e.g. content retrieved via <c>git show</c>) and returns its version information.
    /// The <paramref name="csprojPath"/> is used only to populate <see cref="ProjectVersionInfo.FilePath"/>
    /// and <see cref="ProjectVersionInfo.Name"/>; the file does not need to exist on disk.
    /// Returns <see langword="null"/> when the XML cannot be parsed.
    /// </summary>
    /// <param name="centralPackageVersions">
    /// Optional package id → version map (typically parsed from a <c>Directory.Packages.props</c>
    /// file retrieved from the same git ref as <paramref name="content"/>) used to resolve
    /// &lt;PackageReference&gt; entries that do not declare a version inline. When
    /// <see langword="null"/>, such entries resolve to a <see langword="null"/> version.
    /// </param>
    public ProjectVersionInfo? ParseFromString(
        string content,
        string csprojPath,
        IReadOnlyDictionary<string, string>? centralPackageVersions = null)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(content);
        }
        catch
        {
            return null;
        }

        return BuildInfo(doc, csprojPath, centralPackageVersions);
    }

    // -------------------------------------------------------------------------

    private static ProjectVersionInfo BuildInfo(
        XDocument doc,
        string csprojPath,
        IReadOnlyDictionary<string, string>? centralVersions)
    {
        var name = Path.GetFileNameWithoutExtension(csprojPath);

        return new ProjectVersionInfo
        {
            Name              = name,
            FilePath          = csprojPath,
            Version           = FindFirstElementValue(doc, "Version"),
            VersionPrefix     = FindFirstElementValue(doc, "VersionPrefix"),
            VersionSuffix     = FindFirstElementValue(doc, "VersionSuffix"),
            PackageReferences = ParsePackageReferences(doc, centralVersions),
            ProjectReferences = ParseProjectReferenceNames(doc)
        };
    }

    private static string? FindFirstElementValue(XDocument doc, string localName)
    {
        var value = doc.Descendants()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
            ?.Value;

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Parses all &lt;PackageReference&gt; elements in <paramref name="doc"/>, resolving each
    /// entry's version from an inline <c>Version</c> attribute/element, falling back to
    /// <paramref name="centralVersions"/> (keyed by package id) when not declared inline.
    /// </summary>
    private static IReadOnlyList<PackageReferenceEntry> ParsePackageReferences(
        XDocument doc,
        IReadOnlyDictionary<string, string>? centralVersions)
    {
        var result = new List<PackageReferenceEntry>();

        foreach (var element in doc.Descendants()
                     .Where(e => string.Equals(e.Name.LocalName, "PackageReference", StringComparison.OrdinalIgnoreCase)))
        {
            var id = element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value;
            if (string.IsNullOrWhiteSpace(id))
                continue;
            id = id.Trim();

            var inlineVersion = element.Attribute("Version")?.Value
                ?? element.Elements()
                    .FirstOrDefault(c => string.Equals(c.Name.LocalName, "Version", StringComparison.OrdinalIgnoreCase))
                    ?.Value;
            inlineVersion = string.IsNullOrWhiteSpace(inlineVersion) ? null : inlineVersion.Trim();

            var resolvedVersion = inlineVersion;
            if (resolvedVersion is null && centralVersions is not null && centralVersions.TryGetValue(id, out var central))
                resolvedVersion = central;

            result.Add(new PackageReferenceEntry { Name = id, Version = resolvedVersion });
        }

        return result;
    }

    /// <summary>
    /// Parses all &lt;ProjectReference&gt; elements in <paramref name="doc"/> and returns the
    /// referenced project names (filename without extension, taken from the <c>Include</c> path).
    /// </summary>
    private static IReadOnlyList<string> ParseProjectReferenceNames(XDocument doc)
    {
        return doc.Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "ProjectReference", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => Path.GetFileNameWithoutExtension(v!.Trim().Replace('\\', '/')))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

