using System.Text.RegularExpressions;
using System.Xml.Linq;
using DotnetVersion.Models;

namespace DotnetVersion.Services;

/// <summary>
/// Parses a .csproj XML file into a <see cref="ProjectVersionInfo"/>.
/// </summary>
public sealed class CsprojParser
{
    /// <summary>
    /// Parses the given .csproj file and returns its version information,
    /// or <see langword="null"/> when the file cannot be loaded.
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

        var name = Path.GetFileNameWithoutExtension(csprojPath);

        return new ProjectVersionInfo
        {
            Name         = name,
            FilePath     = csprojPath,
            Version       = FindFirstElementValue(doc, "Version"),
            VersionPrefix = FindFirstElementValue(doc, "VersionPrefix"),
            VersionSuffix = FindFirstElementValue(doc, "VersionSuffix")
        };
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

        var name = Path.GetFileNameWithoutExtension(csprojPath);

        return new ProjectVersionInfo
        {
            Name          = name,
            FilePath      = csprojPath,
            Version       = FindFirstElementValue(doc, "Version"),
            VersionPrefix = FindFirstElementValue(doc, "VersionPrefix"),
            VersionSuffix = FindFirstElementValue(doc, "VersionSuffix")
        };
    }

    /// <summary>
    /// Parses .csproj XML from an in-memory <paramref name="content"/> string
    /// (e.g. content retrieved via <c>git show</c>) and returns its version information.
    /// The <paramref name="csprojPath"/> is used only to populate <see cref="ProjectVersionInfo.FilePath"/>
    /// and <see cref="ProjectVersionInfo.Name"/>; the file does not need to exist on disk.
    /// Returns <see langword="null"/> when the XML cannot be parsed.
    /// </summary>
    public ProjectVersionInfo? ParseFromString(string content, string csprojPath)
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

        var name = Path.GetFileNameWithoutExtension(csprojPath);

        return new ProjectVersionInfo
        {
            Name          = name,
            FilePath      = csprojPath,
            Version       = FindFirstElementValue(doc, "Version"),
            VersionPrefix = FindFirstElementValue(doc, "VersionPrefix"),
            VersionSuffix = FindFirstElementValue(doc, "VersionSuffix")
        };
    }

    // -------------------------------------------------------------------------

    private static string? FindFirstElementValue(XDocument doc, string localName)
    {
        var value = doc.Descendants()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
            ?.Value;

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

