namespace DotnetVersion.Models;

/// <summary>
/// The kind of version change detected for a project by the <c>diff</c> command.
/// </summary>
public enum DiffResultStatus
{
    /// <summary>
    /// The version was bumped relative to the base branch.
    /// </summary>
    Bumped,

    /// <summary>
    /// The project did not exist on the base branch (it is brand-new).
    /// </summary>
    NewProject
}
