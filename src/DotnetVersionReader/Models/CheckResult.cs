namespace DotnetVersion.Models;

/// <summary>
/// Holds the version-bump check result for a single .csproj file.
/// </summary>
public sealed record CheckResult
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

    /// <summary>
    /// Whether the version bump requirement is satisfied for this project.
    /// </summary>
    public required CheckResultStatus Status { get; init; }
}
