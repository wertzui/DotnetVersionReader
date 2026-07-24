using System.Text.Json;
using DotnetVersion.Models;
using DotnetVersion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersion.Tests.Services;

[TestClass]
public sealed class FormatterDiffTests
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
        var result = _formatter.Format([], OutputFormat.Json, Formatter.DiffOptions);
        var array  = JsonSerializer.Deserialize<JsonElement[]>(result)!;
        Assert.AreEqual(0, array.Length);
    }

    [TestMethod]
    public void Format_Json_BumpedResult_SerializesAllFields()
    {
        var results = new List<DiffResult>
        {
            new()
            {
                Name        = "MyLib",
                FilePath    = "/src/MyLib/MyLib.csproj",
                HeadVersion = "2.0.0",
                BaseVersion = "1.0.0",
                Status      = DiffResultStatus.Bumped
            }
        };

        var json  = _formatter.Format(results, OutputFormat.Json, Formatter.DiffOptions);
        var array = JsonSerializer.Deserialize<JsonElement[]>(json)!;

        Assert.AreEqual(1, array.Length);
        Assert.AreEqual("MyLib",   array[0].GetProperty("Name").GetString());
        Assert.AreEqual("2.0.0",   array[0].GetProperty("HeadVersion").GetString());
        Assert.AreEqual("1.0.0",   array[0].GetProperty("BaseVersion").GetString());
        Assert.AreEqual("Bumped",  array[0].GetProperty("Status").GetString());
    }

    [TestMethod]
    public void Format_Json_NewProject_NullBaseVersion_SerializesAsNull()
    {
        var results = new List<DiffResult>
        {
            new()
            {
                Name        = "BrandNewLib",
                FilePath    = "/src/BrandNewLib/BrandNewLib.csproj",
                HeadVersion = "1.0.0",
                BaseVersion = null,
                Status      = DiffResultStatus.NewProject
            }
        };

        var json  = _formatter.Format(results, OutputFormat.Json, Formatter.DiffOptions);
        var array = JsonSerializer.Deserialize<JsonElement[]>(json)!;

        Assert.AreEqual(JsonValueKind.Null, array[0].GetProperty("BaseVersion").ValueKind);
        Assert.AreEqual("NewProject",       array[0].GetProperty("Status").GetString());
    }

    [TestMethod]
    public void Format_Json_MultipleResults_AllPresent()
    {
        var results = new List<DiffResult>
        {
            new() { Name = "Alpha", FilePath = "A.csproj", HeadVersion = "2.0.0", BaseVersion = "1.0.0", Status = DiffResultStatus.Bumped },
            new() { Name = "Beta",  FilePath = "B.csproj", HeadVersion = "1.0.0", BaseVersion = null,    Status = DiffResultStatus.NewProject }
        };

        var json  = _formatter.Format(results, OutputFormat.Json, Formatter.DiffOptions);
        var array = JsonSerializer.Deserialize<JsonElement[]>(json)!;

        Assert.AreEqual(2, array.Length);
    }

    [TestMethod]
    public void Format_Json_DependenciesChanged_SerializesSuggestionAndChanges()
    {
        var results = new List<DiffResult>
        {
            new()
            {
                Name                   = "MyApp",
                FilePath               = "/src/MyApp/MyApp.csproj",
                HeadVersion            = "1.2.3",
                BaseVersion            = "1.2.3",
                Status                 = DiffResultStatus.DependenciesChanged,
                SuggestedVersionPrefix = "1.2.4",
                SuggestedVersionSuffix = "",
                DependencyChanges =
                [
                    new DependencyChange
                    {
                        Kind        = DependencyKind.Package,
                        Name        = "Newtonsoft.Json",
                        BaseVersion = "13.0.1",
                        HeadVersion = "13.0.2",
                        BumpType    = SemVerBumpType.Patch
                    }
                ]
            }
        };

        var json  = _formatter.Format(results, OutputFormat.Json, Formatter.DiffOptions);
        var array = JsonSerializer.Deserialize<JsonElement[]>(json)!;

        Assert.AreEqual("DependenciesChanged", array[0].GetProperty("Status").GetString());
        Assert.AreEqual("1.2.4",               array[0].GetProperty("SuggestedVersionPrefix").GetString());
        Assert.AreEqual("",                    array[0].GetProperty("SuggestedVersionSuffix").GetString());
        Assert.AreEqual("1.2.4",               array[0].GetProperty("SuggestedVersion").GetString());

        var changes = array[0].GetProperty("DependencyChanges");
        Assert.AreEqual(1, changes.GetArrayLength());
        Assert.AreEqual("Package",          changes[0].GetProperty("Kind").GetString());
        Assert.AreEqual("Newtonsoft.Json",   changes[0].GetProperty("Name").GetString());
        Assert.AreEqual("13.0.1",            changes[0].GetProperty("BaseVersion").GetString());
        Assert.AreEqual("13.0.2",            changes[0].GetProperty("HeadVersion").GetString());
        Assert.AreEqual("Patch",             changes[0].GetProperty("BumpType").GetString());
    }

    [TestMethod]
    public void Format_Json_BumpedResult_HasNullSuggestedVersion()
    {
        var results = new List<DiffResult>
        {
            new() { Name = "MyLib", FilePath = "MyLib.csproj", HeadVersion = "2.0.0", BaseVersion = "1.0.0", Status = DiffResultStatus.Bumped }
        };

        var json  = _formatter.Format(results, OutputFormat.Json, Formatter.DiffOptions);
        var array = JsonSerializer.Deserialize<JsonElement[]>(json)!;

        Assert.AreEqual(JsonValueKind.Null, array[0].GetProperty("SuggestedVersion").ValueKind);
    }

    // -------------------------------------------------------------------------
    // Table output
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Format_Table_EmptyList_ReturnsEmpty()
    {
        var result = _formatter.Format([], OutputFormat.Table, Formatter.DiffOptions);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void Format_Table_ContainsExpectedColumns()
    {
        var results = new List<DiffResult>
        {
            new()
            {
                Name        = "MyLib",
                FilePath    = "MyLib.csproj",
                HeadVersion = "2.0.0",
                BaseVersion = "1.0.0",
                Status      = DiffResultStatus.Bumped
            }
        };

        var table = _formatter.Format(results, OutputFormat.Table, Formatter.DiffOptions);

        StringAssert.Contains(table, "Name");
        StringAssert.Contains(table, "HeadVersion");
        StringAssert.Contains(table, "BaseVersion");
        StringAssert.Contains(table, "Status");
        StringAssert.Contains(table, "SuggestedVersion");
    }

    [TestMethod]
    public void Format_Table_DependenciesChanged_ShowsSuggestedVersion()
    {
        var results = new List<DiffResult>
        {
            new()
            {
                Name                   = "MyApp",
                FilePath               = "MyApp.csproj",
                HeadVersion            = "1.2.3",
                BaseVersion            = "1.2.3",
                Status                 = DiffResultStatus.DependenciesChanged,
                SuggestedVersionPrefix = "1.3.0",
                SuggestedVersionSuffix = ""
            }
        };

        var table = _formatter.Format(results, OutputFormat.Table, Formatter.DiffOptions);

        StringAssert.Contains(table, "DependenciesChanged");
        StringAssert.Contains(table, "1.3.0");
    }

    [TestMethod]
    public void Format_Table_BumpedResult_ContainsVersionsAndStatus()
    {
        var results = new List<DiffResult>
        {
            new()
            {
                Name        = "MyLib",
                FilePath    = "MyLib.csproj",
                HeadVersion = "2.0.0",
                BaseVersion = "1.0.0",
                Status      = DiffResultStatus.Bumped
            }
        };

        var table = _formatter.Format(results, OutputFormat.Table, Formatter.DiffOptions);

        StringAssert.Contains(table, "MyLib");
        StringAssert.Contains(table, "2.0.0");
        StringAssert.Contains(table, "1.0.0");
        StringAssert.Contains(table, "Bumped");
    }

    [TestMethod]
    public void Format_Table_NewProject_ShowsNewPlaceholder()
    {
        var results = new List<DiffResult>
        {
            new()
            {
                Name        = "BrandNew",
                FilePath    = "BrandNew.csproj",
                HeadVersion = "1.0.0",
                BaseVersion = null,
                Status      = DiffResultStatus.NewProject
            }
        };

        var table = _formatter.Format(results, OutputFormat.Table, Formatter.DiffOptions);
        StringAssert.Contains(table, "(new)");
    }

    [TestMethod]
    public void Format_Table_ColumnsAreAligned()
    {
        var results = new List<DiffResult>
        {
            new() { Name = "Short",                FilePath = "S.csproj", HeadVersion = "1.0.0",       BaseVersion = "0.9.0", Status = DiffResultStatus.Bumped },
            new() { Name = "AVeryLongProjectName", FilePath = "L.csproj", HeadVersion = "10.0.0-rc.1", BaseVersion = null,    Status = DiffResultStatus.NewProject }
        };

        var output     = _formatter.Format(results, OutputFormat.Table, Formatter.DiffOptions);
        var lines      = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var pipeCounts = lines.Select(l => l.Count(c => c == '|')).Distinct().ToArray();

        Assert.AreEqual(1, pipeCounts.Length,
            $"Expected identical pipe counts on every line, got: [{string.Join(", ", pipeCounts)}]");
    }

    // -------------------------------------------------------------------------
    // Version output
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Format_Version_EmptyList_ReturnsEmpty()
    {
        var result = _formatter.Format([], OutputFormat.Version, Formatter.DiffOptions);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void Format_Version_SingleBumpedProject_ReturnsHeadVersion()
    {
        var results = new List<DiffResult>
        {
            new()
            {
                Name        = "MyLib",
                FilePath    = "MyLib.csproj",
                HeadVersion = "3.0.0",
                BaseVersion = "2.0.0",
                Status      = DiffResultStatus.Bumped
            }
        };

        var version = _formatter.Format(results, OutputFormat.Version, Formatter.DiffOptions);
        Assert.AreEqual("3.0.0", version);
    }

    [TestMethod]
    public void Format_Version_MultipleProjects_Throws()
    {
        var results = new List<DiffResult>
        {
            new() { Name = "A", FilePath = "A.csproj", HeadVersion = "2.0.0", BaseVersion = "1.0.0", Status = DiffResultStatus.Bumped },
            new() { Name = "B", FilePath = "B.csproj", HeadVersion = "1.0.0", BaseVersion = null,    Status = DiffResultStatus.NewProject }
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => _formatter.Format(results, OutputFormat.Version, Formatter.DiffOptions));

        StringAssert.Contains(ex.Message, "A");
        StringAssert.Contains(ex.Message, "B");
    }

    // -------------------------------------------------------------------------
    // List output
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Format_List_EmptyList_ReturnsEmptyString()
    {
        var result = _formatter.Format([], OutputFormat.List, Formatter.DiffOptions);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void Format_List_SingleBumpedItem_ReturnsNameSpaceHeadVersion()
    {
        var results = new List<DiffResult>
        {
            new()
            {
                Name        = "MyLib",
                FilePath    = "MyLib.csproj",
                HeadVersion = "2.0.0",
                BaseVersion = "1.0.0",
                Status      = DiffResultStatus.Bumped
            }
        };

        var output = _formatter.Format(results, OutputFormat.List, Formatter.DiffOptions);
        Assert.AreEqual("MyLib 2.0.0", output);
    }

    [TestMethod]
    public void Format_List_MultipleItems_OneLineEach()
    {
        var results = new List<DiffResult>
        {
            new() { Name = "Alpha", FilePath = "A.csproj", HeadVersion = "2.0.0", BaseVersion = "1.0.0", Status = DiffResultStatus.Bumped },
            new() { Name = "Beta",  FilePath = "B.csproj", HeadVersion = "1.0.0", BaseVersion = null,    Status = DiffResultStatus.NewProject }
        };

        var output = _formatter.Format(results, OutputFormat.List, Formatter.DiffOptions);
        var lines  = output.Split(Environment.NewLine);

        Assert.AreEqual(2, lines.Length);
        Assert.AreEqual("Alpha 2.0.0", lines[0]);
        Assert.AreEqual("Beta 1.0.0",  lines[1]);
    }

    [TestMethod]
    public void Format_List_NoHeadersOrBullets()
    {
        var results = new List<DiffResult>
        {
            new()
            {
                Name        = "MyLib",
                FilePath    = "MyLib.csproj",
                HeadVersion = "2.0.0",
                BaseVersion = "1.0.0",
                Status      = DiffResultStatus.Bumped
            }
        };

        var output = _formatter.Format(results, OutputFormat.List, Formatter.DiffOptions);

        Assert.IsFalse(output.Contains("|"), "List output must not contain table pipes");
        Assert.IsFalse(output.Contains("#"), "List output must not contain headers");
    }
}
