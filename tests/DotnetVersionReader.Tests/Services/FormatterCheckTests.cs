using System.Text.Json;
using DotnetVersion.Models;
using DotnetVersion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersionReader.Tests.Services;

[TestClass]
public sealed class FormatterCheckTests
{
    private Formatter _formatter = null!;

    [TestInitialize]
    public void Setup() => _formatter = new Formatter();

    // -------------------------------------------------------------------------
    // JSON output
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Format_Json_EmptyList_ReturnsEmptyArray()
    {
        var result = _formatter.Format([], OutputFormat.Json, Formatter.CheckOptions);
        var array  = JsonSerializer.Deserialize<JsonElement[]>(result)!;
        Assert.IsEmpty(array);
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

        var json  = _formatter.Format(results, OutputFormat.Json, Formatter.CheckOptions);
        var array = JsonSerializer.Deserialize<JsonElement[]>(json)!;

        Assert.HasCount(1, array);
        Assert.AreEqual("MyLib", array[0].GetProperty("Name").GetString());
        Assert.AreEqual("2.0.0", array[0].GetProperty("HeadVersion").GetString());
        Assert.AreEqual("1.0.0", array[0].GetProperty("BaseVersion").GetString());
        Assert.AreEqual("Ok",    array[0].GetProperty("Status").GetString());
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

        var json  = _formatter.Format(results, OutputFormat.Json, Formatter.CheckOptions);
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

        var json  = _formatter.Format(results, OutputFormat.Json, Formatter.CheckOptions);
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
        var result = _formatter.Format([], OutputFormat.Table, Formatter.CheckOptions);
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

        var table = _formatter.Format(results, OutputFormat.Table, Formatter.CheckOptions);

        Assert.Contains("MyLib", table);
        Assert.Contains("2.0.0", table);
        Assert.Contains("1.0.0", table);
        Assert.Contains("Ok", table);
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

        var table = _formatter.Format(results, OutputFormat.Table, Formatter.CheckOptions);
        Assert.Contains("(new)", table);
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

        var version = _formatter.Format(results, OutputFormat.Version, Formatter.CheckOptions);
        Assert.AreEqual("3.0.0", version);
    }

    [TestMethod]
    public void Format_Version_EmptyList_ReturnsEmpty()
    {
        var result = _formatter.Format([], OutputFormat.Version, Formatter.CheckOptions);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void Format_Version_MultipleProjects_Throws()
    {
        var results = new List<CheckResult>
        {
            new() { Name = "A", FilePath = "A.csproj", HeadVersion = "1.0.0", BaseVersion = "1.0.0", Status = CheckResultStatus.Ok },
            new() { Name = "B", FilePath = "B.csproj", HeadVersion = "2.0.0", BaseVersion = "1.0.0", Status = CheckResultStatus.Ok }
        };

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _formatter.Format(results, OutputFormat.Version, Formatter.CheckOptions));
    }

    [TestMethod]
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

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _formatter.Format(results, OutputFormat.Version, Formatter.CheckOptions));
    }

    // -------------------------------------------------------------------------
    // List output
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Format_List_EmptyList_ReturnsEmptyString()
    {
        var result = _formatter.Format([], OutputFormat.List, Formatter.CheckOptions);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void Format_List_SingleItem_ReturnsNameSpaceHeadVersion()
    {
        var results = new List<CheckResult>
        {
            new()
            {
                Name        = "MyLib",
                FilePath    = "MyLib.csproj",
                HeadVersion = "2.0.0",
                BaseVersion = "1.0.0",
                Status      = CheckResultStatus.Ok
            }
        };

        var output = _formatter.Format(results, OutputFormat.List, Formatter.CheckOptions);
        Assert.AreEqual("MyLib 2.0.0", output);
    }

    [TestMethod]
    public void Format_List_MultipleItems_OneLineEach()
    {
        var results = new List<CheckResult>
        {
            new() { Name = "Alpha", FilePath = "A.csproj", HeadVersion = "1.0.0", BaseVersion = "0.9.0", Status = CheckResultStatus.Ok },
            new() { Name = "Beta",  FilePath = "B.csproj", HeadVersion = "2.0.0", BaseVersion = "2.0.0", Status = CheckResultStatus.BumpRequired }
        };

        var output = _formatter.Format(results, OutputFormat.List, Formatter.CheckOptions);
        var lines  = output.Split(Environment.NewLine);

        Assert.HasCount(2, lines);
        Assert.AreEqual("Alpha 1.0.0", lines[0]);
        Assert.AreEqual("Beta 2.0.0",  lines[1]);
    }
}
