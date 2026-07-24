using DotnetVersion.Models;
using DotnetVersion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersion.Tests.Services;

/// <summary>
/// Unit tests for <see cref="DiffService"/>.
/// These tests verify the core filtering logic: only projects whose version changed
/// (or that are brand-new), or whose dependencies changed, should appear in the results.
/// </summary>
[TestClass]
public sealed class DiffServiceTests
{
    private DiffService _svc = null!;

    [TestInitialize]
    public void Setup() => _svc = new DiffService();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ProjectVersionInfo MakeInfo(
        string name,
        string version,
        IReadOnlyList<PackageReferenceEntry>? packages = null,
        IReadOnlyList<string>? projects = null)
        => new()
        {
            Name              = name,
            FilePath          = $"/src/{name}/{name}.csproj",
            Version           = version,
            PackageReferences = packages ?? [],
            ProjectReferences = projects ?? []
        };

    // -------------------------------------------------------------------------
    // Filtering behaviour (own version change)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void BuildResults_VersionUnchanged_NoDependencyChanges_IsExcluded()
    {
        var results = _svc.BuildResults(
            affectedProjects: [("MyLib", "/src/MyLib/MyLib.csproj")],
            getHeadInfo:      _ => MakeInfo("MyLib", "1.0.0"),
            getBaseInfo:      _ => MakeInfo("MyLib", "1.0.0"));

        Assert.AreEqual(0, results.Count,
            "A project whose version and dependencies have not changed must be excluded from the diff.");
    }

    [TestMethod]
    public void BuildResults_VersionBumped_IsIncluded()
    {
        var results = _svc.BuildResults(
            affectedProjects: [("MyLib", "/src/MyLib/MyLib.csproj")],
            getHeadInfo:      _ => MakeInfo("MyLib", "2.0.0"),
            getBaseInfo:      _ => MakeInfo("MyLib", "1.0.0"));

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(DiffResultStatus.Bumped, results[0].Status);
    }

    [TestMethod]
    public void BuildResults_NewProject_IsIncluded()
    {
        var results = _svc.BuildResults(
            affectedProjects: [("BrandNew", "/src/BrandNew/BrandNew.csproj")],
            getHeadInfo:      _ => MakeInfo("BrandNew", "1.0.0"),
            getBaseInfo:      _ => null);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(DiffResultStatus.NewProject, results[0].Status);
        Assert.IsNull(results[0].BaseVersion);
    }

    [TestMethod]
    public void BuildResults_MixedProjects_OnlyChangedOnesIncluded()
    {
        var headInfos = new Dictionary<string, ProjectVersionInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["Changed"]   = MakeInfo("Changed", "2.0.0"),
            ["Unchanged"] = MakeInfo("Unchanged", "1.0.0"),
            ["BrandNew"]  = MakeInfo("BrandNew", "1.0.0")
        };

        var baseInfos = new Dictionary<string, ProjectVersionInfo?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Changed"]   = MakeInfo("Changed", "1.0.0"),
            ["Unchanged"] = MakeInfo("Unchanged", "1.0.0"),
            ["BrandNew"]  = null
        };

        var results = _svc.BuildResults(
            affectedProjects:
            [
                ("Changed",   "/src/Changed/Changed.csproj"),
                ("Unchanged", "/src/Unchanged/Unchanged.csproj"),
                ("BrandNew",  "/src/BrandNew/BrandNew.csproj")
            ],
            getHeadInfo: name => headInfos[name],
            getBaseInfo: name => baseInfos[name]);

        Assert.AreEqual(2, results.Count,
            "Only 'Changed' and 'BrandNew' should appear; 'Unchanged' must be excluded.");

        var names = results.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.IsTrue(names.Contains("Changed"),  "Changed must be in results");
        Assert.IsTrue(names.Contains("BrandNew"), "BrandNew must be in results");
        Assert.IsFalse(names.Contains("Unchanged"), "Unchanged must NOT be in results");
    }

    [TestMethod]
    public void BuildResults_EmptyAffectedProjects_ReturnsEmptyList()
    {
        var results = _svc.BuildResults(
            affectedProjects: [],
            getHeadInfo:      _ => MakeInfo("X", "1.0.0"),
            getBaseInfo:      _ => MakeInfo("X", "1.0.0"));

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void BuildResults_VersionComparison_IsCaseInsensitive()
    {
        var results = _svc.BuildResults(
            affectedProjects: [("MyLib", "/src/MyLib/MyLib.csproj")],
            getHeadInfo:      _ => MakeInfo("MyLib", "1.0.0-RC.1"),
            getBaseInfo:      _ => MakeInfo("MyLib", "1.0.0-rc.1"));

        Assert.AreEqual(0, results.Count,
            "Version comparison must be case-insensitive; 'RC.1' and 'rc.1' are the same.");
    }

    // -------------------------------------------------------------------------
    // Correct field population
    // -------------------------------------------------------------------------

    [TestMethod]
    public void BuildResults_BumpedProject_FieldsAreCorrect()
    {
        const string path = "/src/MyLib/MyLib.csproj";

        var results = _svc.BuildResults(
            affectedProjects: [("MyLib", path)],
            getHeadInfo:      _ => MakeInfo("MyLib", "3.0.0"),
            getBaseInfo:      _ => MakeInfo("MyLib", "2.0.0"));

        var r = results[0];
        Assert.AreEqual("MyLib",               r.Name);
        Assert.AreEqual(path,                  r.FilePath);
        Assert.AreEqual("3.0.0",               r.HeadVersion);
        Assert.AreEqual("2.0.0",               r.BaseVersion);
        Assert.AreEqual(DiffResultStatus.Bumped, r.Status);
    }

    [TestMethod]
    public void BuildResults_NewProject_FieldsAreCorrect()
    {
        const string path = "/src/New/New.csproj";

        var results = _svc.BuildResults(
            affectedProjects: [("New", path)],
            getHeadInfo:      _ => MakeInfo("New", "1.0.0"),
            getBaseInfo:      _ => null);

        var r = results[0];
        Assert.AreEqual("New",                       r.Name);
        Assert.AreEqual(path,                        r.FilePath);
        Assert.AreEqual("1.0.0",                     r.HeadVersion);
        Assert.IsNull(r.BaseVersion);
        Assert.AreEqual(DiffResultStatus.NewProject, r.Status);
    }

    [TestMethod]
    public void BuildResults_HeadInfoNull_ProjectIsSkipped()
    {
        // If the project cannot be parsed at HEAD, it should be silently skipped
        var results = _svc.BuildResults(
            affectedProjects: [("Ghost", "/src/Ghost/Ghost.csproj")],
            getHeadInfo:      _ => null,
            getBaseInfo:      _ => MakeInfo("Ghost", "1.0.0"));

        Assert.AreEqual(0, results.Count,
            "A project that cannot be parsed at HEAD should be skipped entirely.");
    }

    // -------------------------------------------------------------------------
    // PackageReference changes -> DependenciesChanged + suggested version
    // -------------------------------------------------------------------------

    [TestMethod]
    public void BuildResults_PackageVersionBumped_OwnVersionUnchanged_SuggestsPatchBump()
    {
        var head = MakeInfo("MyApp", "1.2.3", packages: [new PackageReferenceEntry { Name = "Newtonsoft.Json", Version = "13.0.2" }]);
        var @base = MakeInfo("MyApp", "1.2.3", packages: [new PackageReferenceEntry { Name = "Newtonsoft.Json", Version = "13.0.1" }]);

        var results = _svc.BuildResults(
            affectedProjects: [("MyApp", head.FilePath)],
            getHeadInfo:      _ => head,
            getBaseInfo:      _ => @base);

        Assert.AreEqual(1, results.Count);
        var r = results[0];
        Assert.AreEqual(DiffResultStatus.DependenciesChanged, r.Status);
        Assert.AreEqual(1, r.DependencyChanges.Count);
        Assert.AreEqual(DependencyKind.Package, r.DependencyChanges[0].Kind);
        Assert.AreEqual("Newtonsoft.Json", r.DependencyChanges[0].Name);
        Assert.AreEqual(SemVerBumpType.Patch, r.DependencyChanges[0].BumpType);
        Assert.AreEqual("1.2.4", r.SuggestedVersionPrefix);
        Assert.AreEqual(string.Empty, r.SuggestedVersionSuffix);
        Assert.AreEqual("1.2.4", r.SuggestedVersion);
    }

    [TestMethod]
    public void BuildResults_PackageMajorBump_SuggestsMajorBump()
    {
        var head = MakeInfo("MyApp", "1.2.3", packages: [new PackageReferenceEntry { Name = "SomePkg", Version = "2.0.0" }]);
        var @base = MakeInfo("MyApp", "1.2.3", packages: [new PackageReferenceEntry { Name = "SomePkg", Version = "1.0.0" }]);

        var results = _svc.BuildResults(
            affectedProjects: [("MyApp", head.FilePath)],
            getHeadInfo:      _ => head,
            getBaseInfo:      _ => @base);

        var r = results[0];
        Assert.AreEqual(SemVerBumpType.Major, r.DependencyChanges[0].BumpType);
        Assert.AreEqual("2.0.0", r.SuggestedVersionPrefix);
    }

    [TestMethod]
    public void BuildResults_PackageMinorBump_SuggestsMinorBump()
    {
        var head = MakeInfo("MyApp", "1.2.3", packages: [new PackageReferenceEntry { Name = "SomePkg", Version = "1.1.0" }]);
        var @base = MakeInfo("MyApp", "1.2.3", packages: [new PackageReferenceEntry { Name = "SomePkg", Version = "1.0.0" }]);

        var results = _svc.BuildResults(
            affectedProjects: [("MyApp", head.FilePath)],
            getHeadInfo:      _ => head,
            getBaseInfo:      _ => @base);

        var r = results[0];
        Assert.AreEqual(SemVerBumpType.Minor, r.DependencyChanges[0].BumpType);
        Assert.AreEqual("1.3.0", r.SuggestedVersionPrefix);
    }

    [TestMethod]
    public void BuildResults_PackageAdded_ClassifiedAsMinor()
    {
        var head = MakeInfo("MyApp", "1.0.0", packages: [new PackageReferenceEntry { Name = "NewPkg", Version = "1.0.0" }]);
        var @base = MakeInfo("MyApp", "1.0.0", packages: []);

        var results = _svc.BuildResults(
            affectedProjects: [("MyApp", head.FilePath)],
            getHeadInfo:      _ => head,
            getBaseInfo:      _ => @base);

        var r = results[0];
        Assert.AreEqual(DiffResultStatus.DependenciesChanged, r.Status);
        Assert.AreEqual(SemVerBumpType.Minor, r.DependencyChanges[0].BumpType);
        Assert.IsNull(r.DependencyChanges[0].BaseVersion);
        Assert.AreEqual("1.0.0", r.DependencyChanges[0].HeadVersion);
        Assert.AreEqual("1.1.0", r.SuggestedVersionPrefix);
    }

    [TestMethod]
    public void BuildResults_PackageRemoved_ClassifiedAsMajor()
    {
        var head = MakeInfo("MyApp", "1.0.0", packages: []);
        var @base = MakeInfo("MyApp", "1.0.0", packages: [new PackageReferenceEntry { Name = "OldPkg", Version = "1.0.0" }]);

        var results = _svc.BuildResults(
            affectedProjects: [("MyApp", head.FilePath)],
            getHeadInfo:      _ => head,
            getBaseInfo:      _ => @base);

        var r = results[0];
        Assert.AreEqual(SemVerBumpType.Major, r.DependencyChanges[0].BumpType);
        Assert.IsNull(r.DependencyChanges[0].HeadVersion);
        Assert.AreEqual("2.0.0", r.SuggestedVersionPrefix);
    }

    [TestMethod]
    public void BuildResults_PackageVersionUnchanged_NoDependencyChangeReported()
    {
        var head = MakeInfo("MyApp", "1.0.0", packages: [new PackageReferenceEntry { Name = "Pkg", Version = "1.0.0" }]);
        var @base = MakeInfo("MyApp", "1.0.0", packages: [new PackageReferenceEntry { Name = "Pkg", Version = "1.0.0" }]);

        var results = _svc.BuildResults(
            affectedProjects: [("MyApp", head.FilePath)],
            getHeadInfo:      _ => head,
            getBaseInfo:      _ => @base);

        Assert.AreEqual(0, results.Count);
    }

    // -------------------------------------------------------------------------
    // ProjectReference changes
    // -------------------------------------------------------------------------

    [TestMethod]
    public void BuildResults_ProjectReferenceAdded_ClassifiedAsMinor()
    {
        var head = MakeInfo("MyApp", "1.0.0", projects: ["NewDependency"]);
        var @base = MakeInfo("MyApp", "1.0.0", projects: []);

        var results = _svc.BuildResults(
            affectedProjects: [("MyApp", head.FilePath)],
            getHeadInfo:      _ => head,
            getBaseInfo:      _ => @base);

        var r = results[0];
        Assert.AreEqual(DependencyKind.Project, r.DependencyChanges[0].Kind);
        Assert.AreEqual(SemVerBumpType.Minor, r.DependencyChanges[0].BumpType);
        Assert.AreEqual("1.1.0", r.SuggestedVersionPrefix);
    }

    [TestMethod]
    public void BuildResults_ProjectReferenceRemoved_ClassifiedAsMajor()
    {
        var head = MakeInfo("MyApp", "1.0.0", projects: []);
        var @base = MakeInfo("MyApp", "1.0.0", projects: ["OldDependency"]);

        var results = _svc.BuildResults(
            affectedProjects: [("MyApp", head.FilePath)],
            getHeadInfo:      _ => head,
            getBaseInfo:      _ => @base);

        var r = results[0];
        Assert.AreEqual(SemVerBumpType.Major, r.DependencyChanges[0].BumpType);
        Assert.AreEqual("2.0.0", r.SuggestedVersionPrefix);
    }

    // -------------------------------------------------------------------------
    // Multiple changes -> most severe wins
    // -------------------------------------------------------------------------

    [TestMethod]
    public void BuildResults_MultipleDependencyChanges_MostSevereWins()
    {
        var head = MakeInfo(
            "MyApp", "1.2.3",
            packages:
            [
                new PackageReferenceEntry { Name = "PatchPkg", Version = "1.0.1" }, // patch
                new PackageReferenceEntry { Name = "MajorPkg", Version = "2.0.0" }  // major
            ]);
        var @base = MakeInfo(
            "MyApp", "1.2.3",
            packages:
            [
                new PackageReferenceEntry { Name = "PatchPkg", Version = "1.0.0" },
                new PackageReferenceEntry { Name = "MajorPkg", Version = "1.0.0" }
            ]);

        var results = _svc.BuildResults(
            affectedProjects: [("MyApp", head.FilePath)],
            getHeadInfo:      _ => head,
            getBaseInfo:      _ => @base);

        var r = results[0];
        Assert.AreEqual(2, r.DependencyChanges.Count);
        Assert.AreEqual("2.0.0", r.SuggestedVersionPrefix,
            "The most severe change (major) must determine the suggested bump.");
    }

    [TestMethod]
    public void BuildResults_VersionAlreadyBumped_DependencyChangesStillReported_NoSuggestion()
    {
        // Author already bumped the project's own version -> Status is Bumped, not
        // DependenciesChanged, and there is no suggestion (none is needed).
        var head = MakeInfo("MyApp", "2.0.0", packages: [new PackageReferenceEntry { Name = "Pkg", Version = "2.0.0" }]);
        var @base = MakeInfo("MyApp", "1.0.0", packages: [new PackageReferenceEntry { Name = "Pkg", Version = "1.0.0" }]);

        var results = _svc.BuildResults(
            affectedProjects: [("MyApp", head.FilePath)],
            getHeadInfo:      _ => head,
            getBaseInfo:      _ => @base);

        var r = results[0];
        Assert.AreEqual(DiffResultStatus.Bumped, r.Status);
        Assert.AreEqual(1, r.DependencyChanges.Count);
        Assert.IsNull(r.SuggestedVersionPrefix);
        Assert.IsNull(r.SuggestedVersionSuffix);
        Assert.IsNull(r.SuggestedVersion);
    }

    [TestMethod]
    public void BuildResults_NewProject_DependencyChangesEmpty()
    {
        var head = MakeInfo("Brand", "1.0.0", packages: [new PackageReferenceEntry { Name = "Pkg", Version = "1.0.0" }]);

        var results = _svc.BuildResults(
            affectedProjects: [("Brand", head.FilePath)],
            getHeadInfo:      _ => head,
            getBaseInfo:      _ => null);

        Assert.AreEqual(0, results[0].DependencyChanges.Count);
        Assert.IsNull(results[0].SuggestedVersionPrefix);
    }
}
