using DotnetVersion.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersion.Tests.Models;

[TestClass]
public sealed class SemVerTests
{
    // -------------------------------------------------------------------------
    // TryParse
    // -------------------------------------------------------------------------

    [TestMethod]
    public void TryParse_FullVersion_ParsesAllComponents()
    {
        var v = SemVer.TryParse("1.2.3");
        Assert.IsNotNull(v);
        Assert.AreEqual(1, v.Major);
        Assert.AreEqual(2, v.Minor);
        Assert.AreEqual(3, v.Patch);
        Assert.IsNull(v.Suffix);
    }

    [TestMethod]
    public void TryParse_WithSuffix_ParsesSuffix()
    {
        var v = SemVer.TryParse("1.2.3-rc.1");
        Assert.IsNotNull(v);
        Assert.AreEqual("rc.1", v.Suffix);
    }

    [TestMethod]
    public void TryParse_MissingMinorAndPatch_DefaultToZero()
    {
        var v = SemVer.TryParse("5");
        Assert.IsNotNull(v);
        Assert.AreEqual(5, v.Major);
        Assert.AreEqual(0, v.Minor);
        Assert.AreEqual(0, v.Patch);
    }

    [TestMethod]
    public void TryParse_MissingPatch_DefaultsToZero()
    {
        var v = SemVer.TryParse("1.2");
        Assert.IsNotNull(v);
        Assert.AreEqual(1, v.Major);
        Assert.AreEqual(2, v.Minor);
        Assert.AreEqual(0, v.Patch);
    }

    [TestMethod]
    public void TryParse_NullOrEmpty_ReturnsNull()
    {
        Assert.IsNull(SemVer.TryParse(null));
        Assert.IsNull(SemVer.TryParse(""));
        Assert.IsNull(SemVer.TryParse("   "));
    }

    [TestMethod]
    public void TryParse_NonNumericMajor_ReturnsNull()
    {
        Assert.IsNull(SemVer.TryParse("abc.2.3"));
    }

    [TestMethod]
    public void ToString_NoSuffix_ReturnsMajorMinorPatch()
    {
        Assert.AreEqual("1.2.3", new SemVer(1, 2, 3, null).ToString());
    }

    [TestMethod]
    public void ToString_WithSuffix_AppendsDashSuffix()
    {
        Assert.AreEqual("1.2.3-beta.1", new SemVer(1, 2, 3, "beta.1").ToString());
    }

    // -------------------------------------------------------------------------
    // Bump
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Bump_Major_IncrementsMajorResetsRest()
    {
        var bumped = new SemVer(1, 2, 3, "rc.1").Bump(SemVerBumpType.Major);
        Assert.AreEqual(new SemVer(2, 0, 0, null), bumped);
    }

    [TestMethod]
    public void Bump_Minor_IncrementsMinorResetsPatch()
    {
        var bumped = new SemVer(1, 2, 3, "rc.1").Bump(SemVerBumpType.Minor);
        Assert.AreEqual(new SemVer(1, 3, 0, null), bumped);
    }

    [TestMethod]
    public void Bump_Patch_IncrementsPatchOnly()
    {
        var bumped = new SemVer(1, 2, 3, "rc.1").Bump(SemVerBumpType.Patch);
        Assert.AreEqual(new SemVer(1, 2, 4, null), bumped);
    }

    [TestMethod]
    public void Bump_None_ReturnsSameInstance()
    {
        var original = new SemVer(1, 2, 3, "rc.1");
        var bumped   = original.Bump(SemVerBumpType.None);
        Assert.AreEqual(original, bumped);
    }

    [TestMethod]
    public void Bump_AlwaysClearsSuffix()
    {
        var bumped = new SemVer(1, 0, 0, "alpha").Bump(SemVerBumpType.Patch);
        Assert.IsNull(bumped.Suffix);
    }
}
