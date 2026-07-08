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
}
