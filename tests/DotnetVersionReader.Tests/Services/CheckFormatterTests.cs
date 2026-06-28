using System.Text.Json;
using DotnetVersion.Models;
using DotnetVersion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersion.Tests.Services;

[TestClass]
public sealed class CheckFormatterTests
{
    private CheckFormatter _formatter = null!;

    [TestInitialize]
    public void Setup() => _formatter = new CheckFormatter();

    // -------------------------------------------------------------------------
    // JSON output
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Format_Json_EmptyList_ReturnsEmptyArray()
    {
        var result = _formatter.Format([], OutputFormat.Json);
        var array  = JsonSerializer.Deserialize<JsonElement[]>(result)!;
        Assert.AreEqual(0, array.Length);
    }

    [TestMethod]
    public void Format_Json_OkResult_SerializesAllFields()
    {
        var results = new List<CheckResult>
        {
            new()
            {
                Name        = "MyLib",
                FilePath    = "/src/MyLib/MyLib.csproj",
                HeadVersion = "2.0.0",
                BaseVersion = "1.0.0",
                Status      = CheckResultStatus.Ok
            }
        };

        var json  = _formatter.Format(results, OutputFormat.Json);
        var array = JsonSerializer.Deserialize<JsonElement[]>(json)!;

        Assert.AreEqual(1, array.Length);
        Assert.AreEqual("MyLib",  array[0].GetProperty("Name").GetString());
        Assert.AreEqual("2.0.0",  array[0].GetProperty("HeadVersion").GetString());
        Assert.AreEqual("1.0.0",  array[0].GetProperty("BaseVersion").GetString());
        Assert.AreEqual("Ok",     array[0].GetProperty("Status").GetString());
    }

    [TestMethod]
    public void Format_Json_BumpRequiredResult_SerializesStatus()
    {
        var results = new List<CheckResult>
        {
            new()
            {
                Name        = "MyLib",
                FilePath    = "/src/MyLib/MyLib.csproj",
                HeadVersion = "1.0.0",
                BaseVersion = "1.0.0",
                Status      = CheckResultStatus.BumpRequired
            }
        };

        var json  = _formatter.Format(results, OutputFormat.Json);
        var array = JsonSerializer.Deserialize<JsonElement[]>(json)!;

        Assert.AreEqual("BumpRequired", array[0].GetProperty("Status").GetString());
    }

    [TestMethod]
    public void Format_Json_NewProject_NullBaseVersion_SerializesAsNull()
    {
        var results = new List<CheckResult>
        {
            new()
            {
                Name        = "NewLib",
                FilePath    = "/src/NewLib/NewLib.csproj",
                HeadVersion = "1.0.0",
                BaseVersion = null,
                Status      = CheckResultStatus.NewProject
            }
        };

        var json  = _formatter.Format(results, OutputFormat.Json);
        var array = JsonSerializer.Deserialize<JsonElement[]>(json)!;

        Assert.AreEqual(JsonValueKind.Null, array[0].GetProperty("BaseVersion").ValueKind);
        Assert.AreEqual("NewProject",       array[0].GetProperty("Status").GetString());
    }

    // -------------------------------------------------------------------------
    // Table output
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Format_Table_EmptyList_ReturnsEmpty()
    {
        var result = _formatter.Format([], OutputFormat.Table);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void Format_Table_IncludesAllColumns()
    {
        var results = new List<CheckResult>
        {
            new()
            {
                Name        = "MyLib",
                FilePath    = "/src/MyLib/MyLib.csproj",
                HeadVersion = "2.0.0",
                BaseVersion = "1.0.0",
                Status      = CheckResultStatus.Ok
            }
        };

        var table = _formatter.Format(results, OutputFormat.Table);

        StringAssert.Contains(table, "MyLib");
        StringAssert.Contains(table, "2.0.0");
        StringAssert.Contains(table, "1.0.0");
        StringAssert.Contains(table, "Ok");
    }

    [TestMethod]
    public void Format_Table_NewProject_ShowsNewPlaceholder()
    {
        var results = new List<CheckResult>
        {
            new()
            {
                Name        = "BrandNew",
                FilePath    = "/src/BrandNew/BrandNew.csproj",
                HeadVersion = "1.0.0",
                BaseVersion = null,
                Status      = CheckResultStatus.NewProject
            }
        };

        var table = _formatter.Format(results, OutputFormat.Table);
        StringAssert.Contains(table, "(new)");
    }

    // -------------------------------------------------------------------------
    // Version output
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Format_Version_SingleOkProject_ReturnsHeadVersion()
    {
        var results = new List<CheckResult>
        {
            new()
            {
                Name        = "MyLib",
                FilePath    = "/src/MyLib/MyLib.csproj",
                HeadVersion = "3.0.0",
                BaseVersion = "2.0.0",
                Status      = CheckResultStatus.Ok
            }
        };

        var version = _formatter.Format(results, OutputFormat.Version);
        Assert.AreEqual("3.0.0", version);
    }

    [TestMethod]
    public void Format_Version_EmptyList_ReturnsEmpty()
    {
        var result = _formatter.Format([], OutputFormat.Version);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Format_Version_MultipleProjects_Throws()
    {
        var results = new List<CheckResult>
        {
            new() { Name = "A", FilePath = "A.csproj", HeadVersion = "1.0.0", BaseVersion = "1.0.0", Status = CheckResultStatus.Ok },
            new() { Name = "B", FilePath = "B.csproj", HeadVersion = "2.0.0", BaseVersion = "1.0.0", Status = CheckResultStatus.Ok }
        };

        _formatter.Format(results, OutputFormat.Version);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Format_Version_BumpRequired_Throws()
    {
        var results = new List<CheckResult>
        {
            new()
            {
                Name        = "MyLib",
                FilePath    = "/src/MyLib/MyLib.csproj",
                HeadVersion = "1.0.0",
                BaseVersion = "1.0.0",
                Status      = CheckResultStatus.BumpRequired
            }
        };

        _formatter.Format(results, OutputFormat.Version);
    }
}
