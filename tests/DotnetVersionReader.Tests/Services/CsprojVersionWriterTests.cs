using DotnetVersion.Services;
using DotnetVersion.Tests.Fixtures;
using DotnetVersion.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersion.Tests.Services;

[TestClass]
public sealed class CsprojVersionWriterTests
{
    private TempFileHelper _tmp = null!;
    private CsprojVersionWriter _writer = null!;
    private CsprojParser _parser = null!;

    [TestInitialize]
    public void Setup()
    {
        _tmp    = new TempFileHelper();
        _writer = new CsprojVersionWriter();
        _parser = new CsprojParser();
    }

    [TestCleanup]
    public void Cleanup() => _tmp.Dispose();

    // -------------------------------------------------------------------------
    // <Version> element takes precedence, exactly like reading does
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ApplyVersion_ExistingVersionElement_IsUpdatedInPlace()
    {
        var path = _tmp.CreateCsproj(CsprojFixtures.WithVersionOnly, "MyLib");

        _writer.ApplyVersion(path, "4.0.0", "");

        var result = _parser.Parse(path);
        Assert.AreEqual("4.0.0", result!.ResolvedVersion);
        Assert.AreEqual("4.0.0", result.Version);
    }

    [TestMethod]
    public void ApplyVersion_ExistingVersionElement_WithSuffix_CombinesThem()
    {
        var path = _tmp.CreateCsproj(CsprojFixtures.WithVersionOnly, "MyLib");

        _writer.ApplyVersion(path, "4.0.0", "rc.1");

        var result = _parser.Parse(path);
        Assert.AreEqual("4.0.0-rc.1", result!.ResolvedVersion);
    }

    // -------------------------------------------------------------------------
    // <VersionPrefix>/<VersionSuffix> path
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ApplyVersion_ExistingVersionPrefix_IsUpdatedInPlace()
    {
        var path = _tmp.CreateCsproj(CsprojFixtures.WithVersionPrefixOnly, "MyLib");

        _writer.ApplyVersion(path, "3.0.0", "");

        var result = _parser.Parse(path);
        Assert.AreEqual("3.0.0", result!.VersionPrefix);
        Assert.AreEqual("3.0.0", result.ResolvedVersion);
    }

    [TestMethod]
    public void ApplyVersion_PrefixAndSuffix_BothUpdated()
    {
        var path = _tmp.CreateCsproj(CsprojFixtures.WithVersionPrefixAndSuffix, "MyLib");

        _writer.ApplyVersion(path, "2.0.0", "beta.2");

        var result = _parser.Parse(path);
        Assert.AreEqual("2.0.0", result!.VersionPrefix);
        Assert.AreEqual("beta.2", result.VersionSuffix);
        Assert.AreEqual("2.0.0-beta.2", result.ResolvedVersion);
    }

    [TestMethod]
    public void ApplyVersion_EmptySuffix_RemovesExistingVersionSuffixElement()
    {
        var path = _tmp.CreateCsproj(CsprojFixtures.WithVersionPrefixAndSuffix, "MyLib");

        _writer.ApplyVersion(path, "2.0.0", "");

        var result = _parser.Parse(path);
        Assert.AreEqual("2.0.0", result!.ResolvedVersion);
        Assert.IsNull(result.VersionSuffix);
    }

    [TestMethod]
    public void ApplyVersion_NoExistingVersionElements_CreatesVersionPrefix()
    {
        var path = _tmp.CreateCsproj(CsprojFixtures.WithNoVersion, "MyLib");

        _writer.ApplyVersion(path, "1.5.0", "");

        var result = _parser.Parse(path);
        Assert.AreEqual("1.5.0", result!.VersionPrefix);
        Assert.AreEqual("1.5.0", result.ResolvedVersion);
    }

    [TestMethod]
    public void ApplyVersion_NoExistingVersionElements_WithSuffix_CreatesBoth()
    {
        var path = _tmp.CreateCsproj(CsprojFixtures.WithNoVersion, "MyLib");

        _writer.ApplyVersion(path, "1.5.0", "alpha.1");

        var result = _parser.Parse(path);
        Assert.AreEqual("1.5.0", result!.VersionPrefix);
        Assert.AreEqual("alpha.1", result.VersionSuffix);
        Assert.AreEqual("1.5.0-alpha.1", result.ResolvedVersion);
    }

    [TestMethod]
    public void ApplyVersion_NoPropertyGroupAtAll_CreatesOne()
    {
        var path = _tmp.CreateFile(Path.GetTempPath(), $"{Guid.NewGuid():N}.csproj",
            """<Project Sdk="Microsoft.NET.Sdk"></Project>""");

        _writer.ApplyVersion(path, "1.0.0", "");

        var result = _parser.Parse(path);
        Assert.AreEqual("1.0.0", result!.ResolvedVersion);
    }

    // -------------------------------------------------------------------------
    // Other content (unrelated elements) is preserved
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ApplyVersion_PreservesUnrelatedElements()
    {
        var path = _tmp.CreateCsproj(CsprojFixtures.WithTargetFrameworkNet9, "MyApp");

        _writer.ApplyVersion(path, "9.0.0", "");

        var content = File.ReadAllText(path);
        StringAssert.Contains(content, "net9.0");

        var result = _parser.Parse(path);
        Assert.AreEqual("9.0.0", result!.ResolvedVersion);
    }

    [TestMethod]
    public void ApplyVersion_PackageAndProjectReferences_ArePreserved()
    {
        var path = _tmp.CreateCsproj(
            CsprojFixtures.WithDependencies("1.0.0", [("PkgA", "1.0.0")], ["../Lib/Lib.csproj"]),
            "MyApp");

        _writer.ApplyVersion(path, "1.1.0", "");

        var result = _parser.Parse(path);
        Assert.AreEqual("1.1.0", result!.ResolvedVersion);
        Assert.AreEqual(1, result.PackageReferences.Count);
        Assert.AreEqual(1, result.ProjectReferences.Count);
    }

    // -------------------------------------------------------------------------
    // XML declaration must not be introduced (SDK-style .csproj files conventionally
    // have no <?xml ...?> prolog, and adding one is an unwanted diff/behavior change).
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ApplyVersion_FileWithoutXmlDeclaration_DoesNotAddOne()
    {
        var path = _tmp.CreateCsproj(CsprojFixtures.WithVersionOnly, "MyLib");
        var before = File.ReadAllText(path);
        Assert.IsFalse(before.TrimStart().StartsWith("<?xml"), "Fixture must not start with an XML declaration.");

        _writer.ApplyVersion(path, "4.0.0", "");

        var after = File.ReadAllText(path);
        Assert.IsFalse(after.TrimStart().StartsWith("<?xml"),
            "ApplyVersion must not insert an XML declaration that was not present in the original file.");
    }

    [TestMethod]
    public void ApplyVersion_FileWithXmlDeclaration_PreservesIt()
    {
        var path = _tmp.CreateCsproj(
            $"""<?xml version="1.0" encoding="utf-8"?>{Environment.NewLine}{CsprojFixtures.WithVersionOnly}""",
            "MyLib");

        _writer.ApplyVersion(path, "4.0.0", "");

        var after = File.ReadAllText(path);
        Assert.IsTrue(after.TrimStart().StartsWith("<?xml"),
            "ApplyVersion must preserve an XML declaration that was present in the original file.");
    }

    // -------------------------------------------------------------------------
    // Error handling
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ApplyVersion_FileDoesNotExist_ThrowsInvalidOperationException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.csproj");

        Assert.ThrowsExactly<InvalidOperationException>(() => _writer.ApplyVersion(path, "1.0.0", ""));
    }

    [TestMethod]
    public void ApplyVersion_InvalidXml_ThrowsInvalidOperationException()
    {
        var path = _tmp.CreateCsproj("THIS IS NOT XML");

        Assert.ThrowsExactly<InvalidOperationException>(() => _writer.ApplyVersion(path, "1.0.0", ""));
    }
}
