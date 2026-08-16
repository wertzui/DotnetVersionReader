using DotnetVersion.Services;
using DotnetVersionReader.Tests.Fixtures;
using DotnetVersionReader.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersionReader.Tests.Services;

[TestClass]
public sealed class DirectoryPackagesPropsResolverTests
{
    private TempFileHelper _tmp = null!;
    private DirectoryPackagesPropsResolver _resolver = null!;

    [TestInitialize]
    public void Setup()
    {
        _tmp      = new TempFileHelper();
        _resolver = new DirectoryPackagesPropsResolver();
    }

    [TestCleanup]
    public void Cleanup() => _tmp.Dispose();

    // -------------------------------------------------------------------------
    // ParseContent
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ParseContent_ValidProps_ReturnsPackageVersionMap()
    {
        var content = CsprojFixtures.DirectoryPackagesProps(
            [("Newtonsoft.Json", "13.0.1"), ("Serilog", "3.0.0")]);

        var map = _resolver.ParseContent(content);

        Assert.HasCount(2, map);
        Assert.AreEqual("13.0.1", map["Newtonsoft.Json"]);
        Assert.AreEqual("3.0.0",  map["Serilog"]);
    }

    [TestMethod]
    public void ParseContent_LookupIsCaseInsensitive()
    {
        var content = CsprojFixtures.DirectoryPackagesProps([("Newtonsoft.Json", "13.0.1")]);
        var map = _resolver.ParseContent(content);

        Assert.IsTrue(map.ContainsKey("newtonsoft.json"));
    }

    [TestMethod]
    public void ParseContent_InvalidXml_ReturnsEmptyMap()
    {
        var map = _resolver.ParseContent("NOT XML");
        Assert.IsEmpty(map);
    }

    [TestMethod]
    public void ParseContent_NoPackageVersionElements_ReturnsEmptyMap()
    {
        var map = _resolver.ParseContent("<Project><PropertyGroup></PropertyGroup></Project>");
        Assert.IsEmpty(map);
    }

    // -------------------------------------------------------------------------
    // Resolve (file system walk-up)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Resolve_PropsFileInSameDirectory_IsFound()
    {
        var (dir, _) = _tmp.CreateDirectory([]);
        _tmp.CreateFile(dir, "Directory.Packages.props",
            CsprojFixtures.DirectoryPackagesProps([("PkgA", "1.0.0")]));

        var map = _resolver.Resolve(dir);

        Assert.AreEqual("1.0.0", map["PkgA"]);
    }

    [TestMethod]
    public void Resolve_PropsFileInParentDirectory_IsFoundByWalkingUp()
    {
        var (root, _) = _tmp.CreateDirectory([]);
        _tmp.CreateFile(root, "Directory.Packages.props",
            CsprojFixtures.DirectoryPackagesProps([("PkgB", "2.0.0")]));

        var subDir = Path.Combine(root, "src", "MyLib");
        Directory.CreateDirectory(subDir);

        var map = _resolver.Resolve(subDir);

        Assert.AreEqual("2.0.0", map["PkgB"]);
    }

    [TestMethod]
    public void Resolve_NoPropsFileAnywhere_ReturnsEmptyMap()
    {
        var (dir, _) = _tmp.CreateDirectory([]);
        var map = _resolver.Resolve(dir);
        Assert.IsEmpty(map);
    }

    [TestMethod]
    public void Resolve_NearestPropsFileWins_OverAncestor()
    {
        var (root, _) = _tmp.CreateDirectory([]);
        _tmp.CreateFile(root, "Directory.Packages.props",
            CsprojFixtures.DirectoryPackagesProps([("PkgC", "1.0.0")]));

        var subDir = Path.Combine(root, "src");
        Directory.CreateDirectory(subDir);
        _tmp.CreateFile(subDir, "Directory.Packages.props",
            CsprojFixtures.DirectoryPackagesProps([("PkgC", "9.9.9")]));

        var map = _resolver.Resolve(subDir);

        Assert.AreEqual("9.9.9", map["PkgC"],
            "The nearest Directory.Packages.props (in the same directory) must win over an ancestor's.");
    }
}
