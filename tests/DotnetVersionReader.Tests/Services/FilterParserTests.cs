using DotnetVersion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersion.Tests.Services;

[TestClass]
public sealed class FilterParserTests
{
    private FilterParser _parser = null!;

    [TestInitialize]
    public void Setup() => _parser = new FilterParser();

    // -------------------------------------------------------------------------
    // Happy path
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Parse_SingleSimpleFilter_ReturnsTuple()
    {
        var result = _parser.Parse(["GeneratePackageOnBuild=true"]);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("GeneratePackageOnBuild", result[0].Element);
        Assert.IsTrue(result[0].Pattern.IsMatch("true"));
    }

    [TestMethod]
    public void Parse_MultipleFilters_ReturnsAll()
    {
        var result = _parser.Parse(["TargetFramework=net9.0", "GeneratePackageOnBuild=true"]);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void Parse_ValueIsRegex_CompilesSuccessfully()
    {
        var result = _parser.Parse(["TargetFramework=^net[0-9]+\\.0$"]);

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result[0].Pattern.IsMatch("net9.0"));
        Assert.IsFalse(result[0].Pattern.IsMatch("netstandard2.0"));
    }

    [TestMethod]
    public void Parse_EmptyCollection_ReturnsEmptyList()
    {
        var result = _parser.Parse([]);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Parse_ValueContainsEquals_SplitsOnFirstEquals()
    {
        // e.g., "MyProp=a=b" – the value part should be "a=b"
        var result = _parser.Parse(["MyProp=a=b"]);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("MyProp", result[0].Element);
        Assert.IsTrue(result[0].Pattern.IsMatch("a=b"));
    }

    [TestMethod]
    public void Parse_PatternIsMatchCaseInsensitive()
    {
        var result = _parser.Parse(["GeneratePackageOnBuild=TRUE"]);

        Assert.IsTrue(result[0].Pattern.IsMatch("true"));
        Assert.IsTrue(result[0].Pattern.IsMatch("True"));
        Assert.IsTrue(result[0].Pattern.IsMatch("TRUE"));
    }

    // -------------------------------------------------------------------------
    // Error cases
    // -------------------------------------------------------------------------

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Parse_FilterWithNoEquals_ThrowsArgumentException()
    {
        _parser.Parse(["InvalidFilter"]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Parse_FilterWithEqualsAtStart_ThrowsArgumentException()
    {
        _parser.Parse(["=value"]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Parse_InvalidRegexValue_ThrowsArgumentException()
    {
        _parser.Parse(["MyProp=["]);   // unclosed character class
    }
}

