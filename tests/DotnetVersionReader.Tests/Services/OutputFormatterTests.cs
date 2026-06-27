using System.Text.Json;
using DotnetVersion.Models;
using DotnetVersion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersion.Tests.Services;

[TestClass]
public sealed class OutputFormatterTests
{
    private OutputFormatter _formatter = null!;

    [TestInitialize]
    public void Setup() => _formatter = new OutputFormatter();

    // -------------------------------------------------------------------------
    // JSON output
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Format_Json_EmptyList_ReturnsEmptyJsonArray()
    {
        var output = _formatter.Format([], OutputFormat.Json);
        var array  = JsonSerializer.Deserialize<JsonElement[]>(output);
        Assert.IsNotNull(array);
        Assert.AreEqual(0, array.Length);
    }

    [TestMethod]
    public void Format_Json_SingleItem_ContainsNameAndVersion()
    {
        var items  = new[] { MakeInfo("Alpha", "1.0.0") };
        var output = _formatter.Format(items, OutputFormat.Json);

        var array  = JsonSerializer.Deserialize<JsonElement[]>(output)!;
        Assert.AreEqual(1, array.Length);
        Assert.AreEqual("Alpha", array[0].GetProperty("Name").GetString());
        Assert.AreEqual("1.0.0", array[0].GetProperty("Version").GetString());
    }

    [TestMethod]
    public void Format_Json_SingleItem_ContainsMajorMinorPatch()
    {
        var items  = new[] { MakeInfo("Alpha", "3.2.1") };
        var output = _formatter.Format(items, OutputFormat.Json);
        var elem   = JsonSerializer.Deserialize<JsonElement[]>(output)![0];

        Assert.AreEqual(3, elem.GetProperty("Major").GetInt32());
        Assert.AreEqual(2, elem.GetProperty("Minor").GetInt32());
        Assert.AreEqual(1, elem.GetProperty("Patch").GetInt32());
    }

    [TestMethod]
    public void Format_Json_SingleItem_SuffixIsNullWhenAbsent()
    {
        var items  = new[] { MakeInfo("Alpha", "1.0.0") };
        var output = _formatter.Format(items, OutputFormat.Json);
        var elem   = JsonSerializer.Deserialize<JsonElement[]>(output)![0];

        Assert.AreEqual(JsonValueKind.Null, elem.GetProperty("Suffix").ValueKind);
    }

    [TestMethod]
    public void Format_Json_SingleItem_SuffixIsPresentWhenSet()
    {
        var info = new ProjectVersionInfo
        {
            Name          = "Lib",
            FilePath      = "Lib.csproj",
            VersionPrefix = "2.0.0",
            VersionSuffix = "beta.1"
        };
        var output = _formatter.Format([info], OutputFormat.Json);
        var elem   = JsonSerializer.Deserialize<JsonElement[]>(output)![0];

        Assert.AreEqual("beta.1", elem.GetProperty("Suffix").GetString());
    }

    [TestMethod]
    public void Format_Json_MultipleItems_AllPresent()
    {
        var items = new[]
        {
            MakeInfo("Alpha", "1.0.0"),
            MakeInfo("Beta",  "2.0.0-rc.1")
        };
        var output = _formatter.Format(items, OutputFormat.Json);
        var array  = JsonSerializer.Deserialize<JsonElement[]>(output)!;

        Assert.AreEqual(2, array.Length);
    }

    [TestMethod]
    public void Format_Json_UsesResolvedVersion()
    {
        var info = new ProjectVersionInfo
        {
            Name          = "MyLib",
            FilePath      = "MyLib.csproj",
            VersionPrefix = "3.0.0",
            VersionSuffix = "preview.1"
        };
        var output = _formatter.Format([info], OutputFormat.Json);
        var array  = JsonSerializer.Deserialize<JsonElement[]>(output)!;

        Assert.AreEqual("3.0.0-preview.1", array[0].GetProperty("Version").GetString());
    }

    // -------------------------------------------------------------------------
    // Table output
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Format_Table_EmptyList_ReturnsEmptyString()
    {
        var output = _formatter.Format([], OutputFormat.Table);
        Assert.AreEqual(string.Empty, output);
    }

    [TestMethod]
    public void Format_Table_ContainsHeaderLine()
    {
        var items  = new[] { MakeInfo("Alpha", "1.0.0") };
        var output = _formatter.Format(items, OutputFormat.Table);

        StringAssert.Contains(output, "Name");
        StringAssert.Contains(output, "Version");
        StringAssert.Contains(output, "Major");
        StringAssert.Contains(output, "Minor");
        StringAssert.Contains(output, "Patch");
        StringAssert.Contains(output, "Suffix");
    }

    [TestMethod]
    public void Format_Table_ContainsSeparatorLine()
    {
        var items  = new[] { MakeInfo("Alpha", "1.0.0") };
        var output = _formatter.Format(items, OutputFormat.Table);
        var lines  = output.Split(Environment.NewLine);

        // ConsoleTables Markdown: second line is the separator, e.g. "| --- | --- |"
        Assert.IsTrue(lines[1].Replace("-", "").Replace(" ", "").Replace("|", "").Length == 0,
            $"Expected separator line, got: {lines[1]}");
    }

    [TestMethod]
    public void Format_Table_ContainsProjectNameAndVersion()
    {
        var items  = new[] { MakeInfo("MyProject", "2.3.4") };
        var output = _formatter.Format(items, OutputFormat.Table);

        StringAssert.Contains(output, "MyProject");
        StringAssert.Contains(output, "2.3.4");
    }

    [TestMethod]
    public void Format_Table_MultipleItems_AllNamesPresent()
    {
        var items = new[]
        {
            MakeInfo("Alpha", "1.0.0"),
            MakeInfo("Beta",  "2.0.0")
        };
        var output = _formatter.Format(items, OutputFormat.Table);

        StringAssert.Contains(output, "Alpha");
        StringAssert.Contains(output, "Beta");
    }

    [TestMethod]
    public void Format_Table_ColumnsAreAligned()
    {
        var items = new[]
        {
            MakeInfo("ShortName", "1.0.0"),
            MakeInfo("AVeryLongProjectName", "2.0.0-beta.1")
        };
        var output = _formatter.Format(items, OutputFormat.Table);
        var lines  = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // ConsoleTables Markdown output: every line starts and ends with '|'
        // and cells are separated by '|', so all lines should have the same
        // number of pipe characters.
        var pipeCounts = lines.Select(l => l.Count(c => c == '|')).Distinct().ToArray();
        Assert.AreEqual(1, pipeCounts.Length,
            $"Expected identical pipe counts on every line, got: [{string.Join(", ", pipeCounts)}]");
    }

    // -------------------------------------------------------------------------
    // Version output
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Format_Version_EmptyList_ReturnsEmptyString()
    {
        var output = _formatter.Format([], OutputFormat.Version);
        Assert.AreEqual(string.Empty, output);
    }

    [TestMethod]
    public void Format_Version_SingleItem_ReturnsVersionString()
    {
        var output = _formatter.Format([MakeInfo("MyLib", "3.2.1")], OutputFormat.Version);
        Assert.AreEqual("3.2.1", output);
    }

    [TestMethod]
    public void Format_Version_SingleItem_UsesResolvedVersion()
    {
        var info = new ProjectVersionInfo
        {
            Name          = "MyLib",
            FilePath      = "MyLib.csproj",
            VersionPrefix = "2.0.0",
            VersionSuffix = "beta.1"
        };
        var output = _formatter.Format([info], OutputFormat.Version);
        Assert.AreEqual("2.0.0-beta.1", output);
    }

    [TestMethod]
    public void Format_Version_MultipleItems_ThrowsInvalidOperationException()
    {
        var items = new[]
        {
            MakeInfo("Alpha", "1.0.0"),
            MakeInfo("Beta",  "2.0.0")
        };
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => _formatter.Format(items, OutputFormat.Version));

        StringAssert.Contains(ex.Message, "Alpha");
        StringAssert.Contains(ex.Message, "Beta");
        StringAssert.Contains(ex.Message, "2");   // count
    }

    [TestMethod]
    public void Format_Version_MultipleItems_ErrorMessageContainsCount()
    {
        var items = new[]
        {
            MakeInfo("A", "1.0.0"),
            MakeInfo("B", "2.0.0"),
            MakeInfo("C", "3.0.0")
        };
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => _formatter.Format(items, OutputFormat.Version));

        StringAssert.Contains(ex.Message, "3");
    }

    // -------------------------------------------------------------------------

    private static ProjectVersionInfo MakeInfo(string name, string version)
        => new()
        {
            Name     = name,
            FilePath = $"{name}.csproj",
            Version  = version
        };
}

