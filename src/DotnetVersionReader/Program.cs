using System.CommandLine;
using DotnetVersion.Models;
using DotnetVersion.Services;

// ---------------------------------------------------------------------------
// Root command
// ---------------------------------------------------------------------------

var inputArgument = new Argument<string?>(
    name: "input",
    description: "Path to a .csproj, .sln, .slnx file or a folder. Defaults to the current directory.",
    getDefaultValue: () => null);

var outputOption = new Option<OutputFormat>(
    aliases: ["--output", "-o"],
    description: "Output format: json (default), table, or version (single project only).",
    getDefaultValue: () => OutputFormat.Json);

var filterOption = new Option<string[]>(
    aliases: ["--filter", "-f"],
    description: "Filter in the form 'XmlNode=Value' (value may be a regex). Can be specified multiple times.")
{
    AllowMultipleArgumentsPerToken = false,
    Arity = ArgumentArity.ZeroOrMore
};

var schemaOption = new Option<bool>(
    name: "--schema",
    description: "Print the JSON schema for the --output json format and exit.",
    getDefaultValue: () => false);

var rootCommand = new RootCommand("Reads version information from .csproj files.")
{
    inputArgument,
    outputOption,
    filterOption,
    schemaOption
};

rootCommand.SetHandler(async (string? input, OutputFormat output, string[] filters, bool schema) =>
{
    if (schema)
    {
        Console.WriteLine(new JsonSchemaProvider().GetSchema());
        return;
    }

    var locator   = new CsprojLocator();
    var parser    = new CsprojParser();
    var filterParser = new FilterParser();
    var formatter = new OutputFormatter();

    IReadOnlyList<(string, System.Text.RegularExpressions.Regex)> parsedFilters;
    try
    {
        parsedFilters = filterParser.Parse(filters);
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
        return;
    }

    IReadOnlyList<string> csprojFiles;
    try
    {
        csprojFiles = locator.Locate(input);
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
        return;
    }

    var results = new List<ProjectVersionInfo>();
    foreach (var file in csprojFiles)
    {
        var info = parsedFilters.Count > 0
            ? parser.ParseWithFilters(file, parsedFilters)
            : parser.Parse(file);

        if (info is not null)
            results.Add(info);
    }

    string formatted;
    try
    {
        formatted = formatter.Format(results, output);
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
        return;
    }

    Console.WriteLine(formatted);

    await Task.CompletedTask;
},
inputArgument, outputOption, filterOption, schemaOption);

return await rootCommand.InvokeAsync(args);

