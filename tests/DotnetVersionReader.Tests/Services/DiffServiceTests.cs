using DotnetVersion.Models;
using DotnetVersion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersion.Tests.Services;

/// <summary>
/// Unit tests for <see cref="DiffService"/>.
/// These tests verify the core filtering logic: only projects whose version changed
/// (or that are brand-new) should appear in the results.
/// </summary>
[TestClass]
public sealed class DiffServiceTests
{
    private DiffService _svc = null!;

    [TestInitialize]
    public void Setup() => _svc = new DiffService();

    // -------------------------------------------------------------------------
    // Filtering behaviour
    // -------------------------------------------------------------------------

    [TestMethod]
    public void BuildResults_VersionUnchanged_IsExcluded()
    {
        var results = _svc.BuildResults(
            affectedProjects: [("MyLib", "/src/MyLib/MyLib.csproj")],
            getHeadVersion:   _ => "1.0.0",
            getBaseVersion:   _ => "1.0.0");

        Assert.AreEqual(0, results.Count,
            "A project whose version has not changed must be excluded from the diff.");
    }

    [TestMethod]
    public void BuildResults_VersionBumped_IsIncluded()
    {
        var results = _svc.BuildResults(
            affectedProjects: [("MyLib", "/src/MyLib/MyLib.csproj")],
            getHeadVersion:   _ => "2.0.0",
            getBaseVersion:   _ => "1.0.0");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(DiffResultStatus.Bumped, results[0].Status);
    }

    [TestMethod]
    public void BuildResults_NewProject_IsIncluded()
    {
        var results = _svc.BuildResults(
            affectedProjects: [("BrandNew", "/src/BrandNew/BrandNew.csproj")],
            getHeadVersion:   _ => "1.0.0",
            getBaseVersion:   _ => null);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(DiffResultStatus.NewProject, results[0].Status);
        Assert.IsNull(results[0].BaseVersion);
    }

    [TestMethod]
    public void BuildResults_MixedProjects_OnlyChangedOnesIncluded()
    {
        var headVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Changed"]   = "2.0.0",
            ["Unchanged"] = "1.0.0",
            ["BrandNew"]  = "1.0.0"
        };

        var baseVersions = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Changed"]   = "1.0.0",
            ["Unchanged"] = "1.0.0",
            ["BrandNew"]  = null
        };

        var results = _svc.BuildResults(
            affectedProjects:
            [
                ("Changed",   "/src/Changed/Changed.csproj"),
                ("Unchanged", "/src/Unchanged/Unchanged.csproj"),
                ("BrandNew",  "/src/BrandNew/BrandNew.csproj")
            ],
            getHeadVersion: name => headVersions[name],
            getBaseVersion: name => baseVersions[name]);

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
            getHeadVersion:   _ => "1.0.0",
            getBaseVersion:   _ => "1.0.0");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void BuildResults_VersionComparison_IsCaseInsensitive()
    {
        var results = _svc.BuildResults(
            affectedProjects: [("MyLib", "/src/MyLib/MyLib.csproj")],
            getHeadVersion:   _ => "1.0.0-RC.1",
            getBaseVersion:   _ => "1.0.0-rc.1");

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
            getHeadVersion:   _ => "3.0.0",
            getBaseVersion:   _ => "2.0.0");

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
            getHeadVersion:   _ => "1.0.0",
            getBaseVersion:   _ => null);

        var r = results[0];
        Assert.AreEqual("New",                       r.Name);
        Assert.AreEqual(path,                        r.FilePath);
        Assert.AreEqual("1.0.0",                     r.HeadVersion);
        Assert.IsNull(r.BaseVersion);
        Assert.AreEqual(DiffResultStatus.NewProject, r.Status);
    }

    [TestMethod]
    public void BuildResults_HeadVersionNull_ProjectIsSkipped()
    {
        // If the project cannot be parsed at HEAD, it should be silently skipped
        var results = _svc.BuildResults(
            affectedProjects: [("Ghost", "/src/Ghost/Ghost.csproj")],
            getHeadVersion:   _ => null,
            getBaseVersion:   _ => "1.0.0");

        Assert.AreEqual(0, results.Count,
            "A project that cannot be parsed at HEAD should be skipped entirely.");
    }
}
