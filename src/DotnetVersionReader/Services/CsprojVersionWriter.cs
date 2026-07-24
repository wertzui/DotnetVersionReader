using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DotnetVersion.Services;

/// <summary>
/// Writes a resolved version back into a .csproj file on disk, used by <c>diff --bump</c>/
/// <c>diff --fix</c> to automatically apply a suggested version.
/// </summary>
public sealed class CsprojVersionWriter
{
    /// <summary>
    /// Applies <paramref name="versionPrefix"/> (and, if non-empty, <paramref name="versionSuffix"/>)
    /// to the .csproj file at <paramref name="csprojPath"/>, following the same precedence rules
    /// used to resolve a version (see <see cref="Models.ProjectVersionInfo.ResolvedVersion"/>):
    /// <list type="bullet">
    ///   <item>If a &lt;Version&gt; element already exists, it is updated in place with the
    ///         combined version string.</item>
    ///   <item>Otherwise &lt;VersionPrefix&gt; is created/updated with <paramref name="versionPrefix"/>.
    ///         If <paramref name="versionSuffix"/> is empty, any existing &lt;VersionSuffix&gt;
    ///         element is removed; otherwise it is created/updated.</item>
    ///   <item>If neither element exists yet, a new &lt;VersionPrefix&gt; (and, if needed,
    ///         &lt;VersionSuffix&gt;) element is added to the first &lt;PropertyGroup&gt;,
    ///         creating one if the project has none.</item>
    /// </list>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the file cannot be loaded, is not valid XML, or cannot be saved.
    /// </exception>
    public void ApplyVersion(string csprojPath, string versionPrefix, string versionSuffix)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(csprojPath, LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex) when (ex is IOException or System.Xml.XmlException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Could not load '{csprojPath}': {ex.Message}", ex);
        }

        if (doc.Root is null)
            throw new InvalidOperationException($"'{csprojPath}' has no root element.");

        var versionElement = FindFirstElement(doc, "Version");
        if (versionElement is not null)
        {
            versionElement.Value = Combine(versionPrefix, versionSuffix);
        }
        else
        {
            ApplyPrefixAndSuffix(doc, versionPrefix, versionSuffix);
        }

        try
        {
            Save(doc, csprojPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Could not save '{csprojPath}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Saves <paramref name="doc"/> to <paramref name="path"/> without introducing an
    /// <c>&lt;?xml version="1.0" encoding="utf-8"?&gt;</c> declaration when the original file
    /// did not have one. <see cref="XDocument.Save(string)"/> always writes a declaration
    /// regardless of <see cref="XDocument.Declaration"/>, so an explicit <see cref="XmlWriter"/>
    /// with <see cref="XmlWriterSettings.OmitXmlDeclaration"/> is used instead.
    /// </summary>
    private static void Save(XDocument doc, string path)
    {
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = doc.Declaration is null,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };

        using var writer = XmlWriter.Create(path, settings);
        doc.Save(writer);
    }

    // -------------------------------------------------------------------------

    private static void ApplyPrefixAndSuffix(XDocument doc, string versionPrefix, string versionSuffix)
    {
        var prefixElement = FindFirstElement(doc, "VersionPrefix");
        XElement propertyGroup;

        if (prefixElement is not null)
        {
            prefixElement.Value = versionPrefix;
            propertyGroup = prefixElement.Parent!;
        }
        else
        {
            propertyGroup = FindOrCreatePropertyGroup(doc);
            propertyGroup.Add(new XElement("VersionPrefix", versionPrefix));
        }

        var suffixElement = FindFirstElement(doc, "VersionSuffix");
        if (string.IsNullOrEmpty(versionSuffix))
        {
            suffixElement?.Remove();
        }
        else if (suffixElement is not null)
        {
            suffixElement.Value = versionSuffix;
        }
        else
        {
            propertyGroup.Add(new XElement("VersionSuffix", versionSuffix));
        }
    }

    private static XElement FindOrCreatePropertyGroup(XDocument doc)
    {
        var existing = doc.Root!.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "PropertyGroup", StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;

        var created = new XElement("PropertyGroup");
        doc.Root.AddFirst(created);
        return created;
    }

    private static XElement? FindFirstElement(XDocument doc, string localName)
        => doc.Descendants()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

    private static string Combine(string versionPrefix, string versionSuffix)
        => string.IsNullOrEmpty(versionSuffix) ? versionPrefix : $"{versionPrefix}-{versionSuffix}";
}
