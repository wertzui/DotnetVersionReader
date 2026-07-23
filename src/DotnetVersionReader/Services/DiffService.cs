using DotnetVersion.Models;

namespace DotnetVersion.Services;

/// <summary>
/// Builds a list of <see cref="DiffResult"/> from a set of affected projects by comparing
/// head versions to base versions, keeping only projects whose version actually changed.
/// </summary>
public sealed class DiffService
{
    /// <summary>
    /// Compares the head and base version for each entry in <paramref name="affectedProjects"/>
    /// and returns only those whose version changed (bumped or brand-new).
    /// </summary>
    /// <param name="affectedProjects">
    /// Sequence of <c>(name, filePath)</c> pairs representing the projects to compare.
    /// </param>
    /// <param name="getHeadVersion">
    /// Returns the resolved version for a project at HEAD, or <see langword="null"/> if the
    /// project cannot be parsed (it will be skipped).
    /// </param>
    /// <param name="getBaseVersion">
    /// Returns the resolved version for a project on the base ref, or <see langword="null"/>
    /// if the project did not exist there (it is a new project).
    /// </param>
    public IReadOnlyList<DiffResult> BuildResults(
        IEnumerable<(string Name, string FilePath)> affectedProjects,
        Func<string, string?>                       getHeadVersion,
        Func<string, string?>                       getBaseVersion)
    {
        var results = new List<DiffResult>();

        foreach (var (name, filePath) in affectedProjects)
        {
            var headVersion = getHeadVersion(name);

            // If the project can't be parsed at HEAD, skip it entirely
            if (headVersion is null)
                continue;

            var baseVersion = getBaseVersion(name);

            // Skip projects where the version has NOT changed
            if (baseVersion is not null &&
                string.Equals(headVersion, baseVersion, StringComparison.OrdinalIgnoreCase))
                continue;

            results.Add(new DiffResult
            {
                Name        = name,
                FilePath    = filePath,
                HeadVersion = headVersion,
                BaseVersion = baseVersion,
                Status      = baseVersion is null ? DiffResultStatus.NewProject : DiffResultStatus.Bumped
            });
        }

        return results;
    }
}
