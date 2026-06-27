using System.Text.Encodings.Web;
using System.Text.Json;

namespace DotnetVersion.Services;

/// <summary>
/// Returns the JSON Schema that describes the <c>--output json</c> output of this tool.
/// </summary>
public sealed class JsonSchemaProvider
{
    private static readonly JsonSerializerOptions PrettyPrint = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Returns the schema as an indented JSON string.</summary>
    public string GetSchema()
    {
        var schema = new
        {
            @schema = "https://json-schema.org/draft/2020-12/schema",
            title = "DotnetVersion output",
            description = "Array of project version entries produced by dotnet-version --output json.",
            type = "array",
            items = new
            {
                type = "object",
                required = new[] { "Name", "Version" },
                additionalProperties = false,
                properties = new
                {
                    Name = new
                    {
                        type = "string",
                        description = "The project name (filename without extension)."
                    },
                    Version = new
                    {
                        type = "string",
                        description = "The resolved version string."
                    },
                    Major = new
                    {
                        type = new[] { "integer", "null" },
                        description = "Major version component."
                    },
                    Minor = new
                    {
                        type = new[] { "integer", "null" },
                        description = "Minor version component."
                    },
                    Patch = new
                    {
                        type = new[] { "integer", "null" },
                        description = "Patch version component."
                    },
                    Suffix = new
                    {
                        type = new[] { "string", "null" },
                        description = "Pre-release suffix (everything after the first '-'), or null."
                    }
                }
            }
        };

        return JsonSerializer.Serialize(schema, PrettyPrint);
    }
}

