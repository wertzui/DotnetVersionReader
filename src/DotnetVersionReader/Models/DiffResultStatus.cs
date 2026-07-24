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
    NewProject,

    /// <summary>
    /// The project's own version did not change, but at least one of its
    /// &lt;PackageReference&gt; or &lt;ProjectReference&gt; entries did — a version bump may be
    /// warranted. See <see cref="DiffResult.SuggestedVersionPrefix"/> and
    /// <see cref="DiffResult.SuggestedVersionSuffix"/> for the suggested new version.
    /// </summary>
    DependenciesChanged
}
