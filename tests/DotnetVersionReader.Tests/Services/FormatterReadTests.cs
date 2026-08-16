using System.Text.Json;
using DotnetVersion.Models;
using DotnetVersion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersionReader.Tests.Services;

[TestClass]
public sealed class FormatterReadTests
{
    private Formatter _formatter = null!;

    [TestInitialize]
    public void Setup() => _formatter = new Formatter();

    // -------------------------------------------------------------------------
    // JSON output
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Format_Json_EmptyList_ReturnsEmptyJsonArray()
    {
        var output = _formatter.Format([], OutputFormat.Json, Formatter.ReadOptions);
        var array  = JsonSerializer.Deserialize<JsonElement[]>(output);
        Assert.IsNotNull(array);
        Assert.IsEmpty(array);
    }

    [TestMethod]
    public void Format_Json_SingleItem_ContainsNameAndVersion()
    {
        var items  = new[] { MakeInfo("Alpha", "1.0.0") };
        var output = _formatter.Format(items, OutputFormat.Json, Formatter.ReadOptions);

        var array  = JsonSerializer.Deserialize<JsonElement[]>(output)!;
        Assert.HasCount(1, array);
        Assert.AreEqual("Alpha", array[0].GetProperty("Name").GetString());
        Assert.AreEqual("1.0.0", array[0].GetProperty("Version").GetString());
    }

    [TestMethod]
    public void Format_Json_SingleItem_ContainsMajorMinorPatch()
    {
        var items  = new[] { MakeInfo("Alpha", "3.2.1") };
        var output = _formatter.Format(items, OutputFormat.Json, Formatter.ReadOptions);
        var elem   = JsonSerializer.Deserialize<JsonElement[]>(output)![0];

        Assert.AreEqual(3, elem.GetProperty("Major").GetInt32());
        Assert.AreEqual(2, elem.GetProperty("Minor").GetInt32());
        Assert.AreEqual(1, elem.GetProperty("Patch").GetInt32());
    }

    [TestMethod]
    public void Format_Json_SingleItem_SuffixIsNullWhenAbsent()
    {
        var items  = new[] { MakeInfo("Alpha", "1.0.0") };
        var output = _formatter.Format(items, OutputFormat.Json, Formatter.ReadOptions);
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
        var output = _formatter.Format([info], OutputFormat.Json, Formatter.ReadOptions);
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
        var output = _formatter.Format(items, OutputFormat.Json, Formatter.ReadOptions);
        var array  = JsonSerializer.Deserialize<JsonElement[]>(output)!;

        Assert.HasCount(2, array);
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
        var output = _formatter.Format([info], OutputFormat.Json, Formatter.ReadOptions);
        var array  = JsonSerializer.Deserialize<JsonElement[]>(output)!;

        Assert.AreEqual("3.0.0-preview.1", array[0].GetProperty("Version").GetString());
    }

    // -------------------------------------------------------------------------
    // Table output
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Format_Table_EmptyList_ReturnsEmptyString()
    {
        var output = _formatter.Format([], OutputFormat.Table, Formatter.ReadOptions);
        Assert.AreEqual(string.Empty, output);
    }

    [TestMethod]
    public void Format_Table_ContainsHeaderLine()
    {
        var items  = new[] { MakeInfo("Alpha", "1.0.0") };
        var output = _formatter.Format(items, OutputFormat.Table, Formatter.ReadOptions);

        Assert.Contains("Name", output);
        Assert.Contains("Version", output);
        Assert.Contains("Major", output);
        Assert.Contains("Minor", output);
        Assert.Contains("Patch", output);
        Assert.Contains("Suffix", output);
    }

    [TestMethod]
    public void Format_Table_ContainsSeparatorLine()
    {
        var items  = new[] { MakeInfo("Alpha", "1.0.0") };
        var output = _formatter.Format(items, OutputFormat.Table, Formatter.ReadOptions);
        var lines  = output.Split(Environment.NewLine);

        // ConsoleTables Markdown: second line is the separator, e.g. "| --- | --- |"
        Assert.AreEqual(0, lines[1].Replace("-", "").Replace(" ", "").Replace("|", "").Length,
            $"Expected separator line, got: {lines[1]}");
    }

    [TestMethod]
    public void Format_Table_ContainsProjectNameAndVersion()
    {
        var items  = new[] { MakeInfo("MyProject", "2.3.4") };
        var output = _formatter.Format(items, OutputFormat.Table, Formatter.ReadOptions);

        Assert.Contains("MyProject", output);
        Assert.Contains("2.3.4", output);
    }

    [TestMethod]
    public void Format_Table_MultipleItems_AllNamesPresent()
    {
        var items = new[]
        {
            MakeInfo("Alpha", "1.0.0"),
            MakeInfo("Beta",  "2.0.0")
        };
        var output = _formatter.Format(items, OutputFormat.Table, Formatter.ReadOptions);

        Assert.Contains("Alpha", output);
        Assert.Contains("Beta", output);
    }

    [TestMethod]
    public void Format_Table_ColumnsAreAligned()
    {
        var items = new[]
        {
            MakeInfo("ShortName",            "1.0.0"),
            MakeInfo("AVeryLongProjectName", "2.0.0-beta.1")
        };
        var output     = _formatter.Format(items, OutputFormat.Table, Formatter.ReadOptions);
        var lines      = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var pipeCounts = lines.Select(l => l.Count(c => c == '|')).Distinct().ToArray();

        Assert.HasCount(1, pipeCounts,
            $"Expected identical pipe counts on every line, got: [{string.Join(", ", pipeCounts)}]");
    }

    // -------------------------------------------------------------------------
    // Version output
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Format_Version_EmptyList_ReturnsEmptyString()
    {
        var output = _formatter.Format([], OutputFormat.Version, Formatter.ReadOptions);
        Assert.AreEqual(string.Empty, output);
    }

    [TestMethod]
    public void Format_Version_SingleItem_ReturnsVersionString()
    {
        var output = _formatter.Format([MakeInfo("MyLib", "3.2.1")], OutputFormat.Version, Formatter.ReadOptions);
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
        var output = _formatter.Format([info], OutputFormat.Version, Formatter.ReadOptions);
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
        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => _formatter.Format(items, OutputFormat.Version, Formatter.ReadOptions));

        Assert.Contains("Alpha", ex.Message);
        Assert.Contains("Beta", ex.Message);
        Assert.Contains("2", ex.Message);   // count
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
        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => _formatter.Format(items, OutputFormat.Version, Formatter.ReadOptions));

        Assert.Contains("3", ex.Message);
    }

    // -------------------------------------------------------------------------
    // List output
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Format_List_EmptyList_ReturnsEmptyString()
    {
        var output = _formatter.Format([], OutputFormat.List, Formatter.ReadOptions);
        Assert.AreEqual(string.Empty, output);
    }

    [TestMethod]
    public void Format_List_SingleItem_ReturnsNameSpaceVersion()
    {
        var output = _formatter.Format([MakeInfo("MyLib", "1.2.3")], OutputFormat.List, Formatter.ReadOptions);
        Assert.AreEqual("MyLib 1.2.3", output);
    }

    [TestMethod]
    public void Format_List_MultipleItems_OneLineEach()
    {
        var items = new[]
        {
            MakeInfo("Alpha", "1.0.0"),
            MakeInfo("Beta",  "2.0.0-rc.1")
        };
        var output = _formatter.Format(items, OutputFormat.List, Formatter.ReadOptions);
        var lines  = output.Split(Environment.NewLine);

        Assert.HasCount(2, lines);
        Assert.AreEqual("Alpha 1.0.0",     lines[0]);
        Assert.AreEqual("Beta 2.0.0-rc.1", lines[1]);
    }

    [TestMethod]
    public void Format_List_NoHeadersOrBullets()
    {
        var items  = new[] { MakeInfo("MyLib", "1.0.0") };
        var output = _formatter.Format(items, OutputFormat.List, Formatter.ReadOptions);

        Assert.DoesNotContain("|", output,  "List output must not contain table pipes");
        Assert.DoesNotContain("-", output,  "List output must not contain bullets or separators");
        Assert.DoesNotContain("#", output,  "List output must not contain headers");
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
