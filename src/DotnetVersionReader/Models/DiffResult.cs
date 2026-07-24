namespace DotnetVersion.Models;

/// <summary>
/// Holds the version-change information for a single .csproj file as reported by the <c>diff</c> command.
/// Only projects whose version actually changed (or that are brand-new) are represented here.
/// </summary>
public sealed record DiffResult
{
    /// <summary>The project name (filename without extension).</summary>
    public required string Name { get; init; }

    /// <summary>Full path to the .csproj file.</summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// The resolved version as it exists in the current branch (HEAD or the specified head ref).
    /// </summary>
    public required string HeadVersion { get; init; }

    /// <summary>
    /// The resolved version on the base branch, or <see langword="null"/> when the project did
    /// not exist on the base ref (i.e. it is a brand-new project).
    /// </summary>
    public string? BaseVersion { get; init; }

    /// <summary>Whether the project is brand-new or had its version bumped.</summary>
    public required DiffResultStatus Status { get; init; }

    /// <summary>
    /// The individual &lt;PackageReference&gt;/&lt;ProjectReference&gt; changes detected between
    /// <see cref="BaseVersion"/> and <see cref="HeadVersion"/> of this project. Empty for new
    /// projects (there is nothing to diff against).
    /// </summary>
    public IReadOnlyList<DependencyChange> DependencyChanges { get; init; } = [];

    /// <summary>
    /// The suggested value for the &lt;VersionPrefix&gt; element, computed by applying standard
    /// semantic-versioning rules to <see cref="BaseVersion"/> based on the most severe change
    /// found in <see cref="DependencyChanges"/>. <see langword="null"/> when no suggestion
    /// applies (no dependency changes, or the project is brand-new).
    /// </summary>
    public string? SuggestedVersionPrefix { get; init; }

    /// <summary>
    /// The suggested value for the &lt;VersionSuffix&gt; element. Always the empty string when
    /// <see cref="SuggestedVersionPrefix"/> is set (a suggested bump always drops any
    /// pre-release suffix), and <see langword="null"/> when no suggestion applies.
    /// </summary>
    public string? SuggestedVersionSuffix { get; init; }

    /// <summary>
    /// The fully-formatted suggested version string (<see cref="SuggestedVersionPrefix"/>
    /// optionally followed by <c>-</c> and <see cref="SuggestedVersionSuffix"/>), or
    /// <see langword="null"/> when no suggestion applies.
    /// </summary>
    public string? SuggestedVersion => SuggestedVersionPrefix is null
        ? null
        : string.IsNullOrEmpty(SuggestedVersionSuffix)
            ? SuggestedVersionPrefix
            : $"{SuggestedVersionPrefix}-{SuggestedVersionSuffix}";
}
