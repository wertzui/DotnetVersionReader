namespace DotnetVersion.Models;

/// <summary>
/// A minimal semantic-version value (major.minor.patch plus an optional pre-release suffix)
/// used to compare and bump version strings found in .csproj/.props files.
/// </summary>
public sealed record SemVer(int Major, int Minor, int Patch, string? Suffix)
{
    /// <summary>
    /// Attempts to parse <paramref name="version"/> (e.g. <c>"1.2.3"</c> or <c>"1.2.3-rc.1"</c>)
    /// into a <see cref="SemVer"/>. Missing minor/patch components default to <c>0</c>.
    /// Returns <see langword="null"/> when <paramref name="version"/> is empty or its major
    /// component is not a valid integer.
    /// </summary>
    public static SemVer? TryParse(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return null;

        var trimmed = version.Trim();
        var dash = trimmed.IndexOf('-');
        var numericPart = dash >= 0 ? trimmed[..dash] : trimmed;
        var suffixPart = dash >= 0 ? trimmed[(dash + 1)..] : null;

        var parts = numericPart.Split('.');
        if (parts.Length == 0 || !int.TryParse(parts[0], out var major))
            return null;

        var minor = parts.Length > 1 && int.TryParse(parts[1], out var mi) ? mi : 0;
        var patch = parts.Length > 2 && int.TryParse(parts[2], out var pa) ? pa : 0;

        return new SemVer(major, minor, patch, string.IsNullOrWhiteSpace(suffixPart) ? null : suffixPart);
    }

    /// <summary>Renders back to <c>"major.minor.patch"</c> or <c>"major.minor.patch-suffix"</c>.</summary>
    public override string ToString()
        => Suffix is null ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{Suffix}";
    /// <summary>
    /// Returns a new <see cref="SemVer"/> bumped according to <paramref name="bumpType"/>,
    /// following standard semantic-versioning rules:
    /// <list type="bullet">
    ///   <item><see cref="SemVerBumpType.Major"/>: increments major, resets minor/patch to 0.</item>
    ///   <item><see cref="SemVerBumpType.Minor"/>: increments minor, resets patch to 0.</item>
    ///   <item><see cref="SemVerBumpType.Patch"/>: increments patch.</item>
    /// </list>
    /// In every case the pre-release <see cref="Suffix"/> is cleared, since a bumped version
    /// is assumed to be a new, non-prerelease version.
    /// <see cref="SemVerBumpType.None"/> returns this instance unchanged.
    /// </summary>
    public SemVer Bump(SemVerBumpType bumpType) => bumpType switch
    {
        SemVerBumpType.Major => new SemVer(Major + 1, 0, 0, null),
        SemVerBumpType.Minor => new SemVer(Major, Minor + 1, 0, null),
        SemVerBumpType.Patch => new SemVer(Major, Minor, Patch + 1, null),
        _                    => this
    };}
