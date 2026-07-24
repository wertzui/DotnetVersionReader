namespace DotnetVersion.Models;

/// <summary>
/// The kind of semantic-versioning bump implied by a version change.
/// Ordered from least to most significant so that instances can be compared
/// (e.g. via <see cref="Enumerable.Max{TSource}(IEnumerable{TSource})"/>) to find the
/// most severe change among several.
/// </summary>
public enum SemVerBumpType
{
    /// <summary>No version change (or a change that carries no semantic-versioning meaning).</summary>
    None = 0,

    /// <summary>Patch-level change (bug fix, or a pre-release/suffix-only change).</summary>
    Patch = 1,

    /// <summary>Minor-level change (new, backwards-compatible functionality).</summary>
    Minor = 2,

    /// <summary>Major-level change (breaking change).</summary>
    Major = 3
}
