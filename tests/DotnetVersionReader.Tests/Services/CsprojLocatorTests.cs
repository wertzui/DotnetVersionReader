using DotnetVersion.Services;
using DotnetVersion.Tests.Fixtures;
using DotnetVersion.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersion.Tests.Services;

[TestClass]
public sealed class CsprojLocatorTests
{
    private TempFileHelper _tmp = null!;
    private CsprojLocator _locator = null!;

    [TestInitialize]
    public void Setup()
    {
        _tmp     = new TempFileHelper();
        _locator = new CsprojLocator();
    }

    [TestCleanup]
    public void Cleanup() => _tmp.Dispose();

    // -------------------------------------------------------------------------
    // .csproj input
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Locate_CsprojFile_ReturnsThatFileAlone()
    {
        var path   = _tmp.CreateCsproj(CsprojFixtures.WithVersionOnly, "ProjectA");
        var result = _locator.Locate(path);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(path, result[0]);
    }

    // -------------------------------------------------------------------------
    // Directory input
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Locate_Directory_ReturnsAllCsprojFiles()
    {
        var (dir, files) = _tmp.CreateDirectory([
            ("Alpha", CsprojFixtures.WithVersionOnly),
            ("Beta",  CsprojFixtures.WithVersionPrefixOnly)
        ]);

        var result = _locator.Locate(dir);

        Assert.AreEqual(2, result.Count);
        CollectionAssert.AreEquivalent(files.ToList(), result.ToList());
    }

    [TestMethod]
    public void Locate_Directory_SearchesRecursively()
    {
        var (dir, _) = _tmp.CreateDirectory([
            ("Root", CsprojFixtures.WithVersionOnly)
        ]);

        // Create a sub-directory with another project
        var subDir = Path.Combine(dir, "SubDir");
        Directory.CreateDirectory(subDir);
        var subFile = Path.Combine(subDir, "Sub.csproj");
        File.WriteAllText(subFile, CsprojFixtures.WithVersionOnly);

        var result = _locator.Locate(dir);

        Assert.AreEqual(2, result.Count);
    }

    // -------------------------------------------------------------------------
    // .sln input
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Locate_SlnFile_ReturnsProjectsFromSln()
    {
        var p1  = _tmp.CreateCsproj(CsprojFixtures.WithVersionOnly,       "ProjectA");
        var p2  = _tmp.CreateCsproj(CsprojFixtures.WithVersionPrefixOnly, "ProjectB");
        var sln = _tmp.CreateSln([p1, p2]);

        var result = _locator.Locate(sln);

        Assert.AreEqual(2, result.Count);
        CollectionAssert.AreEquivalent(new[] { p1, p2 }, result.ToList());
    }

    [TestMethod]
    public void Locate_SlnFile_IgnoresMissingProjects()
    {
        var p1  = _tmp.CreateCsproj(CsprojFixtures.WithVersionOnly, "ExistsProject");
        var sln = _tmp.CreateSln([p1, Path.Combine(Path.GetTempPath(), "DoesNotExist.csproj")]);

        var result = _locator.Locate(sln);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(p1, result[0]);
    }

    // -------------------------------------------------------------------------
    // .slnx input
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Locate_SlnxFile_ReturnsProjectsFromSlnx()
    {
        var p1   = _tmp.CreateCsproj(CsprojFixtures.WithVersionOnly,       "ProjectX");
        var p2   = _tmp.CreateCsproj(CsprojFixtures.WithVersionPrefixOnly, "ProjectY");
        var slnx = _tmp.CreateSlnx([p1, p2]);

        var result = _locator.Locate(slnx);

        Assert.AreEqual(2, result.Count);
        CollectionAssert.AreEquivalent(new[] { p1, p2 }, result.ToList());
    }

    // -------------------------------------------------------------------------
    // null / empty input → current directory (smoke test)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Locate_NullInput_DoesNotThrow()
    {
        // Just verify it doesn't throw; the current directory may or may not have .csproj files.
        var result = _locator.Locate(null);
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void Locate_EmptyStringInput_DoesNotThrow()
    {
        var result = _locator.Locate(string.Empty);
        Assert.IsNotNull(result);
    }

    // -------------------------------------------------------------------------
    // Error cases
    // -------------------------------------------------------------------------

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Locate_UnsupportedFileExtension_ThrowsArgumentException()
    {
        var txt = Path.Combine(Path.GetTempPath(), "file.txt");
        File.WriteAllText(txt, "content");
        try
        {
            _locator.Locate(txt);
        }
        finally
        {
            File.Delete(txt);
        }
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Locate_NonExistentPath_ThrowsArgumentException()
    {
        _locator.Locate(Path.Combine(Path.GetTempPath(), "does_not_exist_99999"));
    }
}

