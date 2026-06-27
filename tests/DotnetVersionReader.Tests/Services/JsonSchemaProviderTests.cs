using System.Text.Json;
using DotnetVersion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersion.Tests.Services;

[TestClass]
public sealed class JsonSchemaProviderTests
{
    private JsonSchemaProvider _provider = null!;

    [TestInitialize]
    public void Setup() => _provider = new JsonSchemaProvider();

    [TestMethod]
    public void GetSchema_ReturnsValidJson()
    {
        var schema = _provider.GetSchema();
        // Must not throw
        var doc = JsonDocument.Parse(schema);
        Assert.IsNotNull(doc);
    }

    [TestMethod]
    public void GetSchema_RootTypeIsArray()
    {
        var root = JsonDocument.Parse(_provider.GetSchema()).RootElement;
        Assert.AreEqual("array", root.GetProperty("type").GetString());
    }

    [TestMethod]
    public void GetSchema_ItemsHaveAllExpectedProperties()
    {
        var root  = JsonDocument.Parse(_provider.GetSchema()).RootElement;
        var props = root.GetProperty("items").GetProperty("properties");

        foreach (var name in new[] { "Name", "Version", "Major", "Minor", "Patch", "Suffix" })
            Assert.IsTrue(props.TryGetProperty(name, out _), $"Missing '{name}' property");
    }

    [TestMethod]
    public void GetSchema_RequiredContainsNameAndVersion()
    {
        var root     = JsonDocument.Parse(_provider.GetSchema()).RootElement;
        var required = root.GetProperty("items").GetProperty("required")
                           .EnumerateArray()
                           .Select(e => e.GetString())
                           .ToHashSet();

        CollectionAssert.IsSubsetOf(new[] { "Name", "Version" }, required.ToList());
    }

    [TestMethod]
    public void GetSchema_ContainsSchemaKeyword()
    {
        var root = JsonDocument.Parse(_provider.GetSchema()).RootElement;
        // The $schema keyword must be present
        Assert.IsTrue(root.TryGetProperty("$schema", out var schemaProp) ||
                      root.TryGetProperty("schema",  out schemaProp),
            "Expected a schema keyword");
        StringAssert.Contains(schemaProp.GetString(), "json-schema.org");
    }

    [TestMethod]
    public void GetSchema_IsIndented()
    {
        var schema = _provider.GetSchema();
        // Indented JSON contains newlines
        Assert.IsTrue(schema.Contains('\n'), "Expected indented (multi-line) JSON");
    }
}

