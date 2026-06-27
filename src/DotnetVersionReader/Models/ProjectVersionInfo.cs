namespace DotnetVersion.Models;

/// <summary>
/// Holds the parsed version information for a single .csproj file.
/// </summary>
public sealed record ProjectVersionInfo
{
    /// <summary>The project name (filename without extension).</summary>
    public required string Name { get; init; }

    /// <summary>Full path to the .csproj file.</summary>
    public required string FilePath { get; init; }

    /// <summary>Value of the &lt;Version&gt; element, if present.</summary>
    public string? Version { get; init; }

    /// <summary>Value of the &lt;VersionPrefix&gt; element, if present.</summary>
    public string? VersionPrefix { get; init; }

    /// <summary>Value of the &lt;VersionSuffix&gt; element, if present.</summary>
    public string? VersionSuffix { get; init; }

    /// <summary>
    /// The final resolved version, following MSBuild semantics:
    /// <list type="bullet">
    ///   <item>If &lt;Version&gt; is set it takes precedence.</item>
    ///   <item>Otherwise the version is built from &lt;VersionPrefix&gt; (default "1.0.0")
    ///         optionally followed by "-&lt;VersionSuffix&gt;".</item>
    /// </list>
    /// </summary>
    public string ResolvedVersion
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Version))
                return Version.Trim();

            var prefix = string.IsNullOrWhiteSpace(VersionPrefix) ? "1.0.0" : VersionPrefix.Trim();

            return string.IsNullOrWhiteSpace(VersionSuffix)
                ? prefix
                : $"{prefix}-{VersionSuffix.Trim()}";
        }
    }

    /// <summary>
    /// Parses the numeric prefix of <see cref="ResolvedVersion"/> (the part before any '-')
    /// and returns the major component, or <see langword="null"/> if it cannot be parsed.
    /// </summary>
    public int? Major => ParseComponent(ResolvedVersion, 0);

    /// <summary>Minor version component, or <see langword="null"/> if not present.</summary>
    public int? Minor => ParseComponent(ResolvedVersion, 1);

    /// <summary>Patch version component, or <see langword="null"/> if not present.</summary>
    public int? Patch => ParseComponent(ResolvedVersion, 2);

    /// <summary>
    /// The pre-release suffix (everything after the first '-'), or <see langword="null"/>
    /// if the version has no suffix.
    /// </summary>
    public string? ResolvedSuffix
    {
        get
        {
            var v = ResolvedVersion;
            var dash = v.IndexOf('-');
            return dash >= 0 ? v[(dash + 1)..] : null;
        }
    }

    // -------------------------------------------------------------------------

    private static int? ParseComponent(string version, int index)
    {
        // Strip any pre-release suffix before splitting on '.'
        var dash = version.IndexOf('-');
        var numericPart = dash >= 0 ? version[..dash] : version;
        var parts = numericPart.Split('.');
        if (index >= parts.Length)
            return null;
        return int.TryParse(parts[index], out var n) ? n : null;
    }
}

