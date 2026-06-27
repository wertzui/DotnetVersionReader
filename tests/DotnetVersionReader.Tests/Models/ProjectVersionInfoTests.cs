using DotnetVersion.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersion.Tests.Models;

[TestClass]
public sealed class ProjectVersionInfoTests
{
    // -------------------------------------------------------------------------
    // ResolvedVersion logic
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ResolvedVersion_WhenVersionIsSet_ReturnsVersion()
    {
        var info = MakeInfo(version: "3.2.1");
        Assert.AreEqual("3.2.1", info.ResolvedVersion);
    }

    [TestMethod]
    public void ResolvedVersion_WhenVersionIsSet_IgnoresPrefixAndSuffix()
    {
        var info = MakeInfo(version: "9.9.9", versionPrefix: "1.0.0", versionSuffix: "ignored");
        Assert.AreEqual("9.9.9", info.ResolvedVersion);
    }

    [TestMethod]
    public void ResolvedVersion_WhenOnlyPrefixIsSet_ReturnsPrefixAlone()
    {
        var info = MakeInfo(versionPrefix: "2.0.0");
        Assert.AreEqual("2.0.0", info.ResolvedVersion);
    }

    [TestMethod]
    public void ResolvedVersion_WhenPrefixAndSuffixAreSet_ReturnsPrefixDashSuffix()
    {
        var info = MakeInfo(versionPrefix: "1.2.3", versionSuffix: "rc.2");
        Assert.AreEqual("1.2.3-rc.2", info.ResolvedVersion);
    }

    [TestMethod]
    public void ResolvedVersion_WhenOnlySuffixIsSet_UsesDefaultPrefix()
    {
        var info = MakeInfo(versionSuffix: "beta.1");
        Assert.AreEqual("1.0.0-beta.1", info.ResolvedVersion);
    }

    [TestMethod]
    public void ResolvedVersion_WhenNothingIsSet_ReturnsDefaultVersion()
    {
        var info = MakeInfo();
        Assert.AreEqual("1.0.0", info.ResolvedVersion);
    }

    [TestMethod]
    public void ResolvedVersion_TrimsWhitespace()
    {
        var info = MakeInfo(version: "  4.0.0  ");
        Assert.AreEqual("4.0.0", info.ResolvedVersion);
    }

    [TestMethod]
    public void ResolvedVersion_PrefixSuffixTrimsWhitespace()
    {
        var info = MakeInfo(versionPrefix: " 1.0.0 ", versionSuffix: " alpha ");
        Assert.AreEqual("1.0.0-alpha", info.ResolvedVersion);
    }

    // -------------------------------------------------------------------------
    // Major / Minor / Patch / ResolvedSuffix
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Major_ParsesCorrectly()
    {
        Assert.AreEqual(3, MakeInfo(version: "3.2.1").Major);
        Assert.AreEqual(0, MakeInfo(version: "0.1.0").Major);
    }

    [TestMethod]
    public void Minor_ParsesCorrectly()
    {
        Assert.AreEqual(2, MakeInfo(version: "3.2.1").Minor);
        Assert.AreEqual(0, MakeInfo(version: "1.0.0").Minor);
    }

    [TestMethod]
    public void Patch_ParsesCorrectly()
    {
        Assert.AreEqual(1, MakeInfo(version: "3.2.1").Patch);
        Assert.AreEqual(0, MakeInfo(version: "1.0.0").Patch);
    }

    [TestMethod]
    public void Patch_WhenVersionHasTwoComponents_ReturnsNull()
    {
        Assert.IsNull(MakeInfo(version: "1.0").Patch);
    }

    [TestMethod]
    public void ResolvedSuffix_WhenVersionHasSuffix_ReturnsSuffix()
    {
        Assert.AreEqual("rc.1", MakeInfo(version: "1.2.3-rc.1").ResolvedSuffix);
        Assert.AreEqual("beta.1", MakeInfo(versionPrefix: "1.0.0", versionSuffix: "beta.1").ResolvedSuffix);
    }

    [TestMethod]
    public void ResolvedSuffix_WhenVersionHasNoSuffix_ReturnsNull()
    {
        Assert.IsNull(MakeInfo(version: "1.2.3").ResolvedSuffix);
        Assert.IsNull(MakeInfo(versionPrefix: "2.0.0").ResolvedSuffix);
    }

    [TestMethod]
    public void MajorMinorPatch_IgnoreSuffix()
    {
        var info = MakeInfo(version: "4.5.6-preview.3");
        Assert.AreEqual(4, info.Major);
        Assert.AreEqual(5, info.Minor);
        Assert.AreEqual(6, info.Patch);
    }

    [TestMethod]
    public void MajorMinorPatch_WorkWithDefaultVersion()
    {
        var info = MakeInfo(); // resolves to "1.0.0"
        Assert.AreEqual(1, info.Major);
        Assert.AreEqual(0, info.Minor);
        Assert.AreEqual(0, info.Patch);
    }

    // -------------------------------------------------------------------------

    private static ProjectVersionInfo MakeInfo(
        string? version = null,
        string? versionPrefix = null,
        string? versionSuffix = null)
        => new()
        {
            Name          = "TestProject",
            FilePath      = "TestProject.csproj",
            Version       = version,
            VersionPrefix = versionPrefix,
            VersionSuffix = versionSuffix
        };
}

