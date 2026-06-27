using System.Text.Json;
using System.Text.RegularExpressions;
using DotnetVersion.Models;
using DotnetVersion.Services;
using DotnetVersion.Tests.Fixtures;
using DotnetVersion.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersion.Tests.Integration;

/// <summary>
/// End-to-end tests that wire together all services the same way Program.cs does.
/// </summary>
[TestClass]
public sealed class EndToEndTests
{
    private TempFileHelper _tmp = null!;

    [TestInitialize]
    public void Setup() => _tmp = new TempFileHelper();

    [TestCleanup]
    public void Cleanup() => _tmp.Dispose();

    // -------------------------------------------------------------------------

    [TestMethod]
    public void FullPipeline_SingleCsproj_NoFilters_JsonOutput()
    {
        var path   = _tmp.CreateCsproj(CsprojFixtures.WithVersionOnly, "MyApp");
        var result = RunPipeline(path, [], OutputFormat.Json);

        var array = JsonSerializer.Deserialize<JsonElement[]>(result)!;
        Assert.AreEqual(1, array.Length);
        Assert.AreEqual("MyApp",  array[0].GetProperty("Name").GetString());
        Assert.AreEqual("3.2.1",  array[0].GetProperty("Version").GetString());
    }

    [TestMethod]
    public void FullPipeline_SingleCsproj_NoFilters_TableOutput()
    {
        var path   = _tmp.CreateCsproj(CsprojFixtures.WithVersionPrefixAndSuffix, "Core");
        var result = RunPipeline(path, [], OutputFormat.Table);

        StringAssert.Contains(result, "Core");
        StringAssert.Contains(result, "1.2.3-rc.2");
    }

    [TestMethod]
    public void FullPipeline_Directory_NoFilters_ReturnsAllProjects()
    {
        var (dir, _) = _tmp.CreateDirectory([
            ("Alpha", CsprojFixtures.WithVersionOnly),
            ("Beta",  CsprojFixtures.WithVersionPrefixOnly)
        ]);

        var result = RunPipeline(dir, [], OutputFormat.Json);
        var array  = JsonSerializer.Deserialize<JsonElement[]>(result)!;

        Assert.AreEqual(2, array.Length);
    }

    [TestMethod]
    public void FullPipeline_SlnFile_FiltersProjects()
    {
        var p1  = _tmp.CreateCsproj(CsprojFixtures.WithGeneratePackageOnBuildTrue,  "Packable");
        var p2  = _tmp.CreateCsproj(CsprojFixtures.WithGeneratePackageOnBuildFalse, "NotPackable");
        var sln = _tmp.CreateSln([p1, p2]);

        var filters = new FilterParser().Parse(["GeneratePackageOnBuild=^true$"]);
        var result  = RunPipeline(sln, filters, OutputFormat.Json);
        var array   = JsonSerializer.Deserialize<JsonElement[]>(result)!;

        Assert.AreEqual(1, array.Length);
        Assert.AreEqual("Packable", array[0].GetProperty("Name").GetString());
    }

    [TestMethod]
    public void FullPipeline_SlnxFile_ReturnsAllProjects()
    {
        var p1   = _tmp.CreateCsproj(CsprojFixtures.WithVersionOnly,       "X1");
        var p2   = _tmp.CreateCsproj(CsprojFixtures.WithVersionPrefixOnly, "X2");
        var slnx = _tmp.CreateSlnx([p1, p2]);

        var result = RunPipeline(slnx, [], OutputFormat.Json);
        var array  = JsonSerializer.Deserialize<JsonElement[]>(result)!;

        Assert.AreEqual(2, array.Length);
    }

    [TestMethod]
    public void FullPipeline_WithMultipleFilters_AllMustMatch()
    {
        var p1 = _tmp.CreateCsproj(CsprojFixtures.WithTargetFrameworkNet9, "Net9App");
        var p2 = _tmp.CreateCsproj(CsprojFixtures.WithVersionOnly,         "OtherApp");

        var (dir, _) = _tmp.CreateDirectory([]);
        // Write both into the same dir by using the temp directory itself
        var dirPath = Path.GetDirectoryName(p1)!;

        var filters = new FilterParser().Parse(["TargetFramework=net9.0", "Version=4\\.0\\.0"]);

        // Run over individual file
        var result = RunPipeline(p1, filters, OutputFormat.Json);
        var array  = JsonSerializer.Deserialize<JsonElement[]>(result)!;

        Assert.AreEqual(1, array.Length);
        Assert.AreEqual("Net9App", array[0].GetProperty("Name").GetString());
    }

    [TestMethod]
    public void FullPipeline_NoMatchingFilter_ReturnsEmptyArray()
    {
        var path    = _tmp.CreateCsproj(CsprojFixtures.WithVersionOnly, "NoMatch");
        var filters = new FilterParser().Parse(["GeneratePackageOnBuild=true"]);

        var result = RunPipeline(path, filters, OutputFormat.Json);
        var array  = JsonSerializer.Deserialize<JsonElement[]>(result)!;

        Assert.AreEqual(0, array.Length);
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private static string RunPipeline(
        string input,
        IReadOnlyList<(string Element, Regex Pattern)> filters,
        OutputFormat format)
    {
        var locator   = new CsprojLocator();
        var parser    = new CsprojParser();
        var formatter = new OutputFormatter();

        var csprojFiles = locator.Locate(input);
        var results     = new List<ProjectVersionInfo>();

        foreach (var file in csprojFiles)
        {
            var info = filters.Count > 0
                ? parser.ParseWithFilters(file, filters)
                : parser.Parse(file);

            if (info is not null)
                results.Add(info);
        }

        return formatter.Format(results, format);
    }
}

