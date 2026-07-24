using DotnetVersion.Models;

namespace DotnetVersion.Services;

/// <summary>
/// Builds a list of <see cref="DiffResult"/> from a set of affected projects by comparing
/// head versions to base versions, keeping only projects whose version actually changed, and
/// by additionally comparing &lt;PackageReference&gt;/&lt;ProjectReference&gt; entries to detect
/// projects that should be bumped but weren't — suggesting a new &lt;VersionPrefix&gt;/
/// &lt;VersionSuffix&gt; based on semantic versioning.
/// </summary>
public sealed class DiffService
{
    /// <summary>
    /// Compares the head and base <see cref="ProjectVersionInfo"/> for each entry in
    /// <paramref name="affectedProjects"/> and returns only those that changed: either because
    /// the project's own version was bumped, it is brand-new, or — while its own version stayed
    /// the same — one of its &lt;PackageReference&gt;/&lt;ProjectReference&gt; entries changed
    /// (in which case a suggested new version is computed).
    /// </summary>
    /// <param name="affectedProjects">
    /// Sequence of <c>(name, filePath)</c> pairs representing the projects to compare.
    /// </param>
    /// <param name="getHeadInfo">
    /// Returns the parsed project info at HEAD, or <see langword="null"/> if the project cannot
    /// be parsed (it will be skipped).
    /// </param>
    /// <param name="getBaseInfo">
    /// Returns the parsed project info on the base ref, or <see langword="null"/> if the
    /// project did not exist there (it is a new project).
    /// </param>
    public IReadOnlyList<DiffResult> BuildResults(
        IEnumerable<(string Name, string FilePath)> affectedProjects,
        Func<string, ProjectVersionInfo?>           getHeadInfo,
        Func<string, ProjectVersionInfo?>           getBaseInfo)
    {
        var results = new List<DiffResult>();

        foreach (var (name, filePath) in affectedProjects)
        {
            var headInfo = getHeadInfo(name);

            // If the project can't be parsed at HEAD, skip it entirely
            if (headInfo is null)
                continue;

            var headVersion = headInfo.ResolvedVersion;
            var baseInfo    = getBaseInfo(name);
            var baseVersion = baseInfo?.ResolvedVersion;

            // Brand-new project: nothing to diff dependencies against.
            if (baseInfo is null)
            {
                results.Add(new DiffResult
                {
                    Name        = name,
                    FilePath    = filePath,
                    HeadVersion = headVersion,
                    BaseVersion = null,
                    Status      = DiffResultStatus.NewProject
                });
                continue;
            }

            var versionChanged = !string.Equals(headVersion, baseVersion, StringComparison.OrdinalIgnoreCase);
            var dependencyChanges = ComputeDependencyChanges(headInfo, baseInfo);

            if (!versionChanged && dependencyChanges.Count == 0)
                continue; // Nothing changed at all — exclude from the diff.

            if (versionChanged)
            {
                // The project's own version was already bumped by the author; no suggestion needed.
                results.Add(new DiffResult
                {
                    Name              = name,
                    FilePath          = filePath,
                    HeadVersion       = headVersion,
                    BaseVersion       = baseVersion,
                    Status            = DiffResultStatus.Bumped,
                    DependencyChanges = dependencyChanges
                });
                continue;
            }

            // Version unchanged, but dependencies changed: suggest a new version.
            var (suggestedPrefix, suggestedSuffix) = SuggestNextVersion(baseVersion, dependencyChanges);

            results.Add(new DiffResult
            {
                Name                   = name,
                FilePath               = filePath,
                HeadVersion            = headVersion,
                BaseVersion            = baseVersion,
                Status                 = DiffResultStatus.DependenciesChanged,
                DependencyChanges      = dependencyChanges,
                SuggestedVersionPrefix = suggestedPrefix,
                SuggestedVersionSuffix = suggestedSuffix
            });
        }

        return results;
    }

    // -------------------------------------------------------------------------
    // Dependency comparison
    // -------------------------------------------------------------------------

    /// <summary>
    /// Compares the &lt;PackageReference&gt; and &lt;ProjectReference&gt; entries of
    /// <paramref name="headInfo"/> against <paramref name="baseInfo"/> and returns the set of
    /// detected changes (added, removed, or version-bumped).
    /// </summary>
    public static IReadOnlyList<DependencyChange> ComputeDependencyChanges(
        ProjectVersionInfo headInfo,
        ProjectVersionInfo baseInfo)
    {
        var changes = new List<DependencyChange>();

        // --- PackageReference --------------------------------------------------
        var headPackages = headInfo.PackageReferences.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var basePackages = baseInfo.PackageReferences.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var name in headPackages.Keys.Union(basePackages.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var hasHead = headPackages.TryGetValue(name, out var headPkg);
            var hasBase = basePackages.TryGetValue(name, out var basePkg);

            var headVersion = hasHead ? headPkg!.Version : null;
            var baseVersion = hasBase ? basePkg!.Version : null;

            var bumpType = ClassifyChange(hasBase, hasHead, baseVersion, headVersion);
            if (bumpType == SemVerBumpType.None)
                continue;

            changes.Add(new DependencyChange
            {
                Kind        = DependencyKind.Package,
                Name        = name,
                BaseVersion = baseVersion,
                HeadVersion = headVersion,
                BumpType    = bumpType
            });
        }

        // --- ProjectReference ---------------------------------------------------
        var headProjects = headInfo.ProjectReferences.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseProjects = baseInfo.ProjectReferences.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var name in headProjects.Union(baseProjects, StringComparer.OrdinalIgnoreCase))
        {
            var hasHead = headProjects.Contains(name);
            var hasBase = baseProjects.Contains(name);

            if (hasHead == hasBase)
                continue; // present in both, or absent from both — no structural change

            changes.Add(new DependencyChange
            {
                Kind        = DependencyKind.Project,
                Name        = name,
                BaseVersion = hasBase ? name : null,
                HeadVersion = hasHead ? name : null,
                BumpType    = hasHead ? SemVerBumpType.Minor : SemVerBumpType.Major
            });
        }

        return changes;
    }

    /// <summary>
    /// Classifies a single dependency's change into a <see cref="SemVerBumpType"/>:
    /// added → Minor, removed → Major, version bumped → the severity of the semver diff
    /// (falling back to Patch when either version cannot be parsed), unchanged → None.
    /// </summary>
    private static SemVerBumpType ClassifyChange(bool hasBase, bool hasHead, string? baseVersion, string? headVersion)
    {
        if (!hasBase && hasHead)
            return SemVerBumpType.Minor; // added

        if (hasBase && !hasHead)
            return SemVerBumpType.Major; // removed

        if (string.Equals(baseVersion, headVersion, StringComparison.OrdinalIgnoreCase))
            return SemVerBumpType.None; // unchanged (covers both being null too)

        var baseSemVer = SemVer.TryParse(baseVersion);
        var headSemVer = SemVer.TryParse(headVersion);

        if (baseSemVer is null || headSemVer is null)
            return SemVerBumpType.Patch; // can't reliably classify — assume the smallest bump

        if (headSemVer.Major != baseSemVer.Major)
            return SemVerBumpType.Major;
        if (headSemVer.Minor != baseSemVer.Minor)
            return SemVerBumpType.Minor;
        return SemVerBumpType.Patch; // patch or pre-release/suffix-only difference
    }

    // -------------------------------------------------------------------------
    // Version suggestion
    // -------------------------------------------------------------------------

    /// <summary>
    /// Given the project's unchanged base version and the set of detected dependency changes,
    /// computes the suggested new &lt;VersionPrefix&gt; (bumped according to the most severe
    /// change) and &lt;VersionSuffix&gt; (always empty for a suggested bump). Returns
    /// <c>(null, null)</c> when <paramref name="baseVersion"/> cannot be parsed as a semantic
    /// version or there are no dependency changes.
    /// </summary>
    public static (string? Prefix, string? Suffix) SuggestNextVersion(
        string? baseVersion,
        IReadOnlyList<DependencyChange> dependencyChanges)
    {
        if (dependencyChanges.Count == 0)
            return (null, null);

        var mostSevere = dependencyChanges.Max(c => c.BumpType);
        if (mostSevere == SemVerBumpType.None)
            return (null, null);

        var baseSemVer = SemVer.TryParse(baseVersion);
        if (baseSemVer is null)
            return (null, null);

        var bumped = baseSemVer.Bump(mostSevere);
        return (bumped.ToString(), string.Empty);
    }
}
