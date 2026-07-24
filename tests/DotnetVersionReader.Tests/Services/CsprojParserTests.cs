using System.Text.RegularExpressions;
using System.Xml.Linq;
using DotnetVersion.Services;
using DotnetVersion.Tests.Fixtures;
using DotnetVersion.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersion.Tests.Services;

[TestClass]
public sealed class CsprojParserTests
{
    private TempFileHelper _tmp = null!;
    private CsprojParser _parser = null!;

    [TestInitialize]
    public void Setup()
    {
        _tmp    = new TempFileHelper();
        _parser = new CsprojParser();
    }

    [TestCleanup]
    public void Cleanup() => _tmp.Dispose();

    // -------------------------------------------------------------------------
    // Parse – version extraction
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Parse_WithVersionOnly_ExtractsVersion()
    {
        var path = _tmp.CreateCsproj(CsprojFixtures.WithVersionOnly, "MyLib");
        var result = _parser.Parse(path);

        Assert.IsNotNull(result);
        Assert.AreEqual("MyLib",  result.Name);
        Assert.AreEqual("3.2.1", result.Version);
        Assert.IsNull(result.VersionPrefix);
        Assert.IsNull(result.VersionSuffix);
        Assert.AreEqual("3.2.1", result.ResolvedVersion);
    }

    [TestMethod]
    public void Parse_WithVersionPrefixOnly_ExtractsPrefix()
    {
        var path = _tmp.CreateCsproj(CsprojFixtures.WithVersionPrefixOnly);
        var result = _parser.Parse(path);

        Assert.IsNotNull(result);
        Assert.AreEqual("2.0.0", result.VersionPrefix);
        Assert.IsNull(result.Version);
        Assert.AreEqual("2.0.0", result.ResolvedVersion);
    }

    [TestMethod]
    public void Parse_WithVersionSuffixOnly_UsesFallbackPrefix()
    {
        var path = _tmp.CreateCsproj(CsprojFixtures.WithVersionSuffixOnly);
        var result = _parser.Parse(path);

        Assert.IsNotNull(result);
        Assert.AreEqual("beta.1", result.VersionSuffix);
        Assert.AreEqual("1.0.0-beta.1", result.ResolvedVersion);
    }

    [TestMethod]
    public void Parse_WithVersionPrefixAndSuffix_CombinesThem()
    {
        var path = _tmp.CreateCsproj(CsprojFixtures.WithVersionPrefixAndSuffix);
        var result = _parser.Parse(path);

        Assert.IsNotNull(result);
        Assert.AreEqual("1.2.3-rc.2", result.ResolvedVersion);
    }

    [TestMethod]
    public void Parse_WhenVersionAndPrefixSuffixExist_VersionTakesPrecedence()
    {
        var path = _tmp.CreateCsproj(CsprojFixtures.WithVersionAndPrefixSuffix);
        var result = _parser.Parse(path);

        Assert.IsNotNull(result);
        Assert.AreEqual("9.9.9", result.ResolvedVersion);
    }

    [TestMethod]
    public void Parse_WithNoVersion_ReturnsDefaultFallback()
    {
        var path = _tmp.CreateCsproj(CsprojFixtures.WithNoVersion);
        var result = _parser.Parse(path);

        Assert.IsNotNull(result);
        Assert.AreEqual("1.0.0", result.ResolvedVersion);
    }

    [TestMethod]
    public void Parse_SetsFilePathAndName()
    {
        var path = _tmp.CreateCsproj(CsprojFixtures.WithVersionOnly, "Awesome.Library");
        var result = _parser.Parse(path);

        Assert.IsNotNull(result);
        Assert.AreEqual("Awesome.Library", result.Name);
        Assert.AreEqual(path, result.FilePath);
    }

    [TestMethod]
    public void Parse_WhenFileNotFound_ReturnsNull()
    {
        var result = _parser.Parse(Path.Combine(Path.GetTempPath(), "nonexistent_99999.csproj"));
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Parse_WhenFileIsInvalidXml_ReturnsNull()
    {
        var path = _tmp.CreateCsproj("THIS IS NOT XML");
        var result = _parser.Parse(path);
        Assert.IsNull(result);
    }

    // -------------------------------------------------------------------------
    // MatchesFilter
    // -------------------------------------------------------------------------

    [TestMethod]
    public void MatchesFilter_WhenElementMatchesExactValue_ReturnsTrue()
    {
        var doc     = XDocument.Parse(CsprojFixtures.WithGeneratePackageOnBuildTrue);
        var pattern = new Regex("^true$", RegexOptions.IgnoreCase);
        Assert.IsTrue(_parser.MatchesFilter(doc, "GeneratePackageOnBuild", pattern));
    }

    [TestMethod]
    public void MatchesFilter_WhenElementValueDoesNotMatch_ReturnsFalse()
    {
        var doc     = XDocument.Parse(CsprojFixtures.WithGeneratePackageOnBuildFalse);
        var pattern = new Regex("^true$", RegexOptions.IgnoreCase);
        Assert.IsFalse(_parser.MatchesFilter(doc, "GeneratePackageOnBuild", pattern));
    }

    [TestMethod]
    public void MatchesFilter_WhenElementDoesNotExist_ReturnsFalse()
    {
        var doc     = XDocument.Parse(CsprojFixtures.WithNoVersion);
        var pattern = new Regex("true");
        Assert.IsFalse(_parser.MatchesFilter(doc, "GeneratePackageOnBuild", pattern));
    }

    [TestMethod]
    public void MatchesFilter_WhenElementIsDeeplyNested_ReturnsTrue()
    {
        var doc     = XDocument.Parse(CsprojFixtures.WithDeeplyNestedProperty);
        var pattern = new Regex("true", RegexOptions.IgnoreCase);
        Assert.IsTrue(_parser.MatchesFilter(doc, "GeneratePackageOnBuild", pattern));
    }

    [TestMethod]
    public void MatchesFilter_IsCaseInsensitiveForElementName()
    {
        var doc     = XDocument.Parse(CsprojFixtures.WithGeneratePackageOnBuildTrue);
        var pattern = new Regex("true", RegexOptions.IgnoreCase);
        // Use different casing for element name
        Assert.IsTrue(_parser.MatchesFilter(doc, "generatepackageonbuild", pattern));
    }

    [TestMethod]
    public void MatchesFilter_SupportsRegexPattern()
    {
        var doc     = XDocument.Parse(CsprojFixtures.WithTargetFrameworkNet9);
        var pattern = new Regex("^net[0-9]+\\.0$", RegexOptions.IgnoreCase);
        Assert.IsTrue(_parser.MatchesFilter(doc, "TargetFramework", pattern));
    }

    // -------------------------------------------------------------------------
    // ParseWithFilters
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ParseWithFilters_WhenAllFiltersMatch_ReturnsInfo()
    {
        var path    = _tmp.CreateCsproj(CsprojFixtures.WithGeneratePackageOnBuildTrue, "Pack");
        var filters = new List<(string, Regex)>
        {
            ("GeneratePackageOnBuild", new Regex("true", RegexOptions.IgnoreCase))
        };

        var result = _parser.ParseWithFilters(path, filters);

        Assert.IsNotNull(result);
        Assert.AreEqual("Pack", result.Name);
    }

    [TestMethod]
    public void ParseWithFilters_WhenOneFilterDoesNotMatch_ReturnsNull()
    {
        var path    = _tmp.CreateCsproj(CsprojFixtures.WithGeneratePackageOnBuildFalse);
        var filters = new List<(string, Regex)>
        {
            ("GeneratePackageOnBuild", new Regex("^true$", RegexOptions.IgnoreCase))
        };

        var result = _parser.ParseWithFilters(path, filters);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseWithFilters_WithMultipleMatchingFilters_ReturnsInfo()
    {
        var path    = _tmp.CreateCsproj(CsprojFixtures.WithTargetFrameworkNet9, "MultiFilter");
        var filters = new List<(string, Regex)>
        {
            ("TargetFramework", new Regex("net9")),
            ("Version",         new Regex("4\\.0\\.0"))
        };

        var result = _parser.ParseWithFilters(path, filters);

        Assert.IsNotNull(result);
        Assert.AreEqual("4.0.0", result.ResolvedVersion);
    }

    [TestMethod]
    public void ParseWithFilters_WithNoFilters_ReturnsInfo()
    {
        var path   = _tmp.CreateCsproj(CsprojFixtures.WithVersionOnly);
        var result = _parser.ParseWithFilters(path, []);

        Assert.IsNotNull(result);
        Assert.AreEqual("3.2.1", result.ResolvedVersion);
    }

    // -------------------------------------------------------------------------
    // PackageReference parsing
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Parse_WithInlinePackageReferenceVersion_ResolvesFromAttribute()
    {
        var (dir, _) = _tmp.CreateDirectory([]);
        var path = _tmp.CreateFile(dir, "MyApp.csproj",
            CsprojFixtures.WithPackageReference("Newtonsoft.Json", "13.0.1"));

        var result = _parser.Parse(path);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.PackageReferences.Count);
        Assert.AreEqual("Newtonsoft.Json", result.PackageReferences[0].Name);
        Assert.AreEqual("13.0.1",          result.PackageReferences[0].Version);
    }

    [TestMethod]
    public void Parse_PackageReferenceWithNoInlineVersion_ResolvesFromDirectoryPackagesProps()
    {
        var (dir, _) = _tmp.CreateDirectory([]);
        _tmp.CreateFile(dir, "Directory.Packages.props",
            CsprojFixtures.DirectoryPackagesProps([("Newtonsoft.Json", "13.0.2")]));
        var path = _tmp.CreateFile(dir, "MyApp.csproj",
            CsprojFixtures.WithCentrallyManagedPackageReference("Newtonsoft.Json"));

        var result = _parser.Parse(path);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.PackageReferences.Count);
        Assert.AreEqual("13.0.2", result.PackageReferences[0].Version);
    }

    [TestMethod]
    public void Parse_PackageReferenceWithNoInlineVersionAndNoPropsFile_ResolvesToNull()
    {
        var (dir, _) = _tmp.CreateDirectory([]);
        var path = _tmp.CreateFile(dir, "MyApp.csproj",
            CsprojFixtures.WithCentrallyManagedPackageReference("SomePkg"));

        var result = _parser.Parse(path);

        Assert.IsNotNull(result);
        Assert.IsNull(result.PackageReferences[0].Version);
    }

    [TestMethod]
    public void Parse_PackageReferenceInParentDirectoryPropsFile_IsResolvedByWalkingUp()
    {
        var (root, _) = _tmp.CreateDirectory([]);
        _tmp.CreateFile(root, "Directory.Packages.props",
            CsprojFixtures.DirectoryPackagesProps([("PkgX", "5.0.0")]));

        var projectDir = Path.Combine(root, "src", "MyApp");
        Directory.CreateDirectory(projectDir);
        var path = Path.Combine(projectDir, "MyApp.csproj");
        File.WriteAllText(path, CsprojFixtures.WithCentrallyManagedPackageReference("PkgX"));

        var result = _parser.Parse(path);

        Assert.IsNotNull(result);
        Assert.AreEqual("5.0.0", result.PackageReferences[0].Version);
    }

    [TestMethod]
    public void Parse_InlineVersionTakesPrecedenceOverCentrallyManagedVersion()
    {
        var (dir, _) = _tmp.CreateDirectory([]);
        _tmp.CreateFile(dir, "Directory.Packages.props",
            CsprojFixtures.DirectoryPackagesProps([("Pkg", "1.0.0")]));
        var path = _tmp.CreateFile(dir, "MyApp.csproj",
            CsprojFixtures.WithPackageReference("Pkg", "9.9.9"));

        var result = _parser.Parse(path);

        Assert.AreEqual("9.9.9", result!.PackageReferences[0].Version);
    }

    [TestMethod]
    public void Parse_MultiplePackageReferences_AllParsed()
    {
        var (dir, _) = _tmp.CreateDirectory([]);
        var path = _tmp.CreateFile(dir, "MyApp.csproj",
            CsprojFixtures.WithDependencies("1.0.0",
                packages: [("PkgA", "1.0.0"), ("PkgB", "2.0.0")],
                projectRefs: []));

        var result = _parser.Parse(path);

        Assert.AreEqual(2, result!.PackageReferences.Count);
    }

    // -------------------------------------------------------------------------
    // ProjectReference parsing
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Parse_ProjectReference_ExtractsProjectNameFromIncludePath()
    {
        var (dir, _) = _tmp.CreateDirectory([]);
        var path = _tmp.CreateFile(dir, "MyApp.csproj",
            CsprojFixtures.WithProjectReference("../MyLib/MyLib.csproj"));

        var result = _parser.Parse(path);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.ProjectReferences.Count);
        Assert.AreEqual("MyLib", result.ProjectReferences[0]);
    }

    [TestMethod]
    public void Parse_MultipleProjectReferences_AllParsed()
    {
        var (dir, _) = _tmp.CreateDirectory([]);
        var path = _tmp.CreateFile(dir, "MyApp.csproj",
            CsprojFixtures.WithDependencies("1.0.0",
                packages: [],
                projectRefs: ["../Lib1/Lib1.csproj", "../Lib2/Lib2.csproj"]));

        var result = _parser.Parse(path);

        Assert.AreEqual(2, result!.ProjectReferences.Count);
        Assert.IsTrue(result.ProjectReferences.Contains("Lib1"));
        Assert.IsTrue(result.ProjectReferences.Contains("Lib2"));
    }

    [TestMethod]
    public void Parse_NoPackageOrProjectReferences_ReturnsEmptyLists()
    {
        var path   = _tmp.CreateCsproj(CsprojFixtures.WithVersionOnly);
        var result = _parser.Parse(path);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.PackageReferences.Count);
        Assert.AreEqual(0, result.ProjectReferences.Count);
    }
}

