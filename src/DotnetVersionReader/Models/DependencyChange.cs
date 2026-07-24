namespace DotnetVersion.Models;

/// <summary>
/// Describes a version change (added, removed, or bumped) detected for a single
/// <c>&lt;PackageReference&gt;</c> or <c>&lt;ProjectReference&gt;</c> between the base and head
/// state of a project.
/// </summary>
public sealed record DependencyChange
{
    /// <summary>Whether this is a NuGet package or a project reference.</summary>
    public required DependencyKind Kind { get; init; }

    /// <summary>The package id (for <see cref="DependencyKind.Package"/>) or project name.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The resolved version on the base ref, or <see langword="null"/> when the dependency
    /// did not exist there (i.e. it was added).
    /// </summary>
    public string? BaseVersion { get; init; }

    /// <summary>
    /// The resolved version at head, or <see langword="null"/> when the dependency no longer
    /// exists there (i.e. it was removed).
    /// </summary>
    public string? HeadVersion { get; init; }

    /// <summary>
    /// The semantic-versioning severity implied by this change:
    /// <see cref="SemVerBumpType.Major"/> for a removed dependency or a major version bump,
    /// <see cref="SemVerBumpType.Minor"/> for an added dependency or a minor version bump,
    /// <see cref="SemVerBumpType.Patch"/> for a patch or pre-release/suffix-only change.
    /// </summary>
    public required SemVerBumpType BumpType { get; init; }
}
