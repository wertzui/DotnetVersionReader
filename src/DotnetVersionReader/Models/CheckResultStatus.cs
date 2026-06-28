namespace DotnetVersion.Models;

/// <summary>
/// The outcome of a version-bump check for a single project.
/// </summary>
public enum CheckResultStatus
{
    /// <summary>
    /// No relevant files changed, or the version was bumped relative to the base branch.
    /// </summary>
    Ok,

    /// <summary>
    /// Relevant files changed but the version is identical to the base branch – a bump is required.
    /// </summary>
    BumpRequired,

    /// <summary>
    /// The project did not exist on the base branch (it is brand-new). No bump is required.
    /// </summary>
    NewProject
}
