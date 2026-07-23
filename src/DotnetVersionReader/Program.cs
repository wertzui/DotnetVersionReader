using System.CommandLine;
using System.Text.RegularExpressions;
using DotnetVersion.Models;
using DotnetVersion.Services;

// ---------------------------------------------------------------------------
// Shared option / argument factories
// Each call returns a NEW instance (System.CommandLine does not allow the same
// instance to be added to more than one command), but all instances are
// configured identically so the UX is uniform across commands.
// ---------------------------------------------------------------------------

static Option<string?> MakeInputOption() => new("--input", "-i")
{
    Description = "Path to a .csproj, .sln, .slnx file or a folder. Defaults to the current directory."
};

static Option<OutputFormat> MakeOutputOption() => new("--output", "-o")
{
    Description = "Output format: json (default), table, or version (single project only).",
    DefaultValueFactory = _ => OutputFormat.Json
};

static Option<string[]> MakeFilterOption() => new Option<string[]>("--filter", "-f")
{
    Description = "Filter in the form 'XmlNode=Value' (value may be a regex). Can be specified multiple times.",
    AllowMultipleArgumentsPerToken = false,
    Arity = ArgumentArity.ZeroOrMore
};

// ---------------------------------------------------------------------------
// Shared helpers (locate + filter, used by both commands)
// ---------------------------------------------------------------------------

static IReadOnlyList<(string Element, Regex Pattern)> ParseFilters(string[] filters)
{
    try
    {
        return new FilterParser().Parse(filters);
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(2);
        return [];
    }
}

/// <summary>
/// Locates .csproj files from <paramref name="input"/> and applies
/// <paramref name="parsedFilters"/>. Returns the matching file list, or exits
/// with code 2 if no files are found.
/// </summary>
static IReadOnlyList<string> LocateAndFilter(
    string? input,
    IReadOnlyList<(string Element, Regex Pattern)> parsedFilters,
    bool requireNonEmpty = true)
{
    IReadOnlyList<string> files;
    try
    {
        files = new CsprojLocator().Locate(input);
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(2);
        return [];
    }

    if (parsedFilters.Count > 0)
    {
        var parser = new CsprojParser();
        files = files
            .Where(f => parser.ParseWithFilters(f, parsedFilters) is not null)
            .ToList();
    }

    if (requireNonEmpty && files.Count == 0)
    {
        Console.Error.WriteLine("No .csproj files found matching the specified filters.");
        Environment.Exit(2);
        return [];
    }

    return files;
}

// ---------------------------------------------------------------------------
// Root command  (shows help; subcommands do the real work)
// ---------------------------------------------------------------------------

var rootInputOption  = MakeInputOption();
var rootOutputOption = MakeOutputOption();
var rootFilterOption = MakeFilterOption();

var rootCommand = new RootCommand(
    "Reads and checks version information from .csproj, .sln and .slnx files.")
{
    rootInputOption,
    rootOutputOption,
    rootFilterOption
};

// ---------------------------------------------------------------------------
// `read` subcommand  (default: read / display versions)
// ---------------------------------------------------------------------------

var readSchemaOption = new Option<bool>("--schema")
{
    Description = "Print the JSON schema for the --output json format and exit."
};

var readInputOption  = MakeInputOption();
var readOutputOption = MakeOutputOption();
var readFilterOption = MakeFilterOption();

var readCommand = new Command(
    "read",
    "Reads and displays version information from .csproj files. This is the default command.")
{
    readInputOption,
    readOutputOption,
    readFilterOption,
    readSchemaOption
};

static async Task RunRead(string? input, OutputFormat output, string[] filters, bool schema)
{
    if (schema)
    {
        Console.WriteLine(new JsonSchemaProvider().GetSchema());
        return;
    }

    var parsedFilters = ParseFilters(filters);
    var csprojFiles   = LocateAndFilter(input, parsedFilters, requireNonEmpty: false);
    var parser        = new CsprojParser();
    var formatter     = new Formatter();

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
        formatted = formatter.Format(results, output, Formatter.ReadOptions);
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(2);
        return;
    }

    Console.WriteLine(formatted);
    await Task.CompletedTask;
}

readCommand.SetAction((parseResult, _) => RunRead(
    parseResult.GetValue(readInputOption),
    parseResult.GetValue(readOutputOption),
    parseResult.GetValue(readFilterOption) ?? [],
    parseResult.GetValue(readSchemaOption)));

// ---------------------------------------------------------------------------
// `check` subcommand  (verify version bumps in a PR / branch diff)
// ---------------------------------------------------------------------------

var checkInputOption  = MakeInputOption();
var checkOutputOption = MakeOutputOption();
var checkFilterOption = MakeFilterOption();

var baseRefOption = new Option<string>("--base", "-b")
{
    Description = "The git ref to compare against. All projects whose files differ between this ref and --head will be checked.",
    DefaultValueFactory = _ => "origin/main"
};

var headRefOption = new Option<string>("--head")
{
    Description = "The git ref representing the current (PR) state. Defaults to HEAD.",
    DefaultValueFactory = _ => "HEAD"
};

var checkCommand = new Command(
    "check",
    "Checks that every project whose source files have changed (relative to --base) has had its version bumped. Exits with code 1 if any project requires a bump, code 2 on usage errors.")
{
    checkInputOption,
    baseRefOption,
    headRefOption,
    checkOutputOption,
    checkFilterOption
};

checkCommand.SetAction(async (parseResult, _) =>
{
    string?      input   = parseResult.GetValue(checkInputOption);
    string       baseRef = parseResult.GetValue(baseRefOption)!;
    string       headRef = parseResult.GetValue(headRefOption)!;
    OutputFormat output  = parseResult.GetValue(checkOutputOption);
    string[]     filters = parseResult.GetValue(checkFilterOption) ?? [];
    var parser    = new CsprojParser();
    var graphSvc  = new DependencyGraphService();
    var gitSvc    = new GitService(parser);
    var formatter = new Formatter();

    // 1. Locate and filter .csproj files
    var parsedFilters = ParseFilters(filters);
    var csprojFiles   = LocateAndFilter(input, parsedFilters, requireNonEmpty: true);

    // 2. Determine the repository root
    string repoRoot;
    try
    {
        var anyProjectDir = Path.GetDirectoryName(csprojFiles[0]) ?? Directory.GetCurrentDirectory();
        repoRoot = gitSvc.GetRepositoryRoot(anyProjectDir);
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(2);
        return;
    }

    // 3. Determine which files changed
    IReadOnlyList<string> changedFiles;
    try
    {
        changedFiles = gitSvc.GetChangedFiles(baseRef, headRef, repoRoot);
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(2);
        return;
    }

    // 4. Build dependency graph and find affected projects
    var graph            = graphSvc.Build(csprojFiles);
    var affectedProjects = graphSvc.GetAffectedProjects(changedFiles, graph);

    // 5. For each affected project compare head vs base version
    var results = new List<CheckResult>();
    foreach (var node in affectedProjects)
    {
        var headInfo = parser.Parse(node.CsprojPath);
        if (headInfo is null)
            continue;

        var headVersion = headInfo.ResolvedVersion;
        var baseVersion = gitSvc.GetVersionAtRef(baseRef, node.CsprojPath, repoRoot);

        CheckResultStatus status;
        if (baseVersion is null)
            status = CheckResultStatus.NewProject;
        else if (string.Equals(headVersion, baseVersion, StringComparison.OrdinalIgnoreCase))
            status = CheckResultStatus.BumpRequired;
        else
            status = CheckResultStatus.Ok;

        results.Add(new CheckResult
        {
            Name        = headInfo.Name,
            FilePath    = node.CsprojPath,
            HeadVersion = headVersion,
            BaseVersion = baseVersion,
            Status      = status
        });
    }

    // 6. Format and print
    string formatted;
    try
    {
        formatted = formatter.Format(results, output, Formatter.CheckOptions);
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
        return;
    }

    Console.WriteLine(formatted);

    // 7. Exit with code 1 if any project needs a bump
    if (results.Any(r => r.Status == CheckResultStatus.BumpRequired))
        Environment.Exit(1);

    await Task.CompletedTask;
});

// ---------------------------------------------------------------------------
// `diff` subcommand  (show projects whose version changed relative to a base branch)
// ---------------------------------------------------------------------------

var diffInputOption  = MakeInputOption();
var diffOutputOption = MakeOutputOption();
var diffFilterOption = MakeFilterOption();

var diffBaseRefOption = new Option<string>("--base", "-b")
{
    Description = "The git ref to compare against. Defaults to origin/main.",
    DefaultValueFactory = _ => "origin/main"
};

var diffHeadRefOption = new Option<string>("--head")
{
    Description = "The git ref representing the current state. Defaults to HEAD.",
    DefaultValueFactory = _ => "HEAD"
};

var diffCommand = new Command(
    "diff",
    "Shows projects whose version has changed (or that are new) relative to --base. Does not exit with a non-zero code based on results.")
{
    diffInputOption,
    diffBaseRefOption,
    diffHeadRefOption,
    diffOutputOption,
    diffFilterOption
};

diffCommand.SetAction(async (parseResult, _) =>
{
    string?      input   = parseResult.GetValue(diffInputOption);
    string       baseRef = parseResult.GetValue(diffBaseRefOption)!;
    string       headRef = parseResult.GetValue(diffHeadRefOption)!;
    OutputFormat output  = parseResult.GetValue(diffOutputOption);
    string[]     filters = parseResult.GetValue(diffFilterOption) ?? [];
    var parser    = new CsprojParser();
    var graphSvc  = new DependencyGraphService();
    var gitSvc    = new GitService(parser);
    var diffSvc   = new DiffService();
    var formatter = new Formatter();

    // 1. Locate and filter .csproj files
    var parsedFilters = ParseFilters(filters);
    var csprojFiles   = LocateAndFilter(input, parsedFilters, requireNonEmpty: true);

    // 2. Determine the repository root
    string repoRoot;
    try
    {
        var anyProjectDir = Path.GetDirectoryName(csprojFiles[0]) ?? Directory.GetCurrentDirectory();
        repoRoot = gitSvc.GetRepositoryRoot(anyProjectDir);
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(2);
        return;
    }

    // 3. Determine which files changed
    IReadOnlyList<string> changedFiles;
    try
    {
        changedFiles = gitSvc.GetChangedFiles(baseRef, headRef, repoRoot);
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(2);
        return;
    }

    // 4. Build dependency graph and find affected projects
    var graph            = graphSvc.Build(csprojFiles);
    var affectedProjects = graphSvc.GetAffectedProjects(changedFiles, graph);

    // 5. Parse head info for all affected projects once; build name-keyed lookups
    var headInfoByName = new Dictionary<string, (string FilePath, string HeadVersion)>(StringComparer.OrdinalIgnoreCase);
    foreach (var node in affectedProjects)
    {
        var headInfo = parser.Parse(node.CsprojPath);
        if (headInfo is null)
            continue;
        headInfoByName[headInfo.Name] = (node.CsprojPath, headInfo.ResolvedVersion);
    }

    // 6. For each parsed project compare head vs base version; keep only those that changed
    var results = diffSvc.BuildResults(
        affectedProjects: headInfoByName.Select(kvp => (kvp.Key, kvp.Value.FilePath)),
        getHeadVersion:   name => headInfoByName.TryGetValue(name, out var h) ? h.HeadVersion : null,
        getBaseVersion:   name => headInfoByName.TryGetValue(name, out var h)
                                      ? gitSvc.GetVersionAtRef(baseRef, h.FilePath, repoRoot)
                                      : null);

    // 6. Format and print
    string formatted;
    try
    {
        formatted = formatter.Format(results, output, Formatter.DiffOptions);
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(2);
        return;
    }

    Console.WriteLine(formatted);
    await Task.CompletedTask;
});

rootCommand.Subcommands.Add(readCommand);
rootCommand.Subcommands.Add(checkCommand);
rootCommand.Subcommands.Add(diffCommand);

// ---------------------------------------------------------------------------
// Default-subcommand injection
// ---------------------------------------------------------------------------
// When the first non-option token is NOT a known subcommand name, prepend "read"
// so that `dotnet-version [<input>] [options]` maps to `dotnet-version read [<input>] [options]`.
// This gives the usage shape: dotnet-version [command] [<input>] [options]
// while still allowing bare `dotnet-version` (no args) to work.

var knownCommands = new HashSet<string>(
    rootCommand.Subcommands.Select(c => c.Name),
    StringComparer.OrdinalIgnoreCase);

// Root-level flags that must NOT trigger subcommand injection
var rootOnlyFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "--help", "-h", "-?", "--version"
};

var effectiveArgs = args;
var firstToken = args.Length > 0 ? args[0] : null;
var isRootFlag  = firstToken is not null && rootOnlyFlags.Contains(firstToken);
var isSubCmd    = firstToken is not null && knownCommands.Contains(firstToken);

if (!isRootFlag && !isSubCmd)
{
    // No subcommand and no root-level flag → inject `read` as the default subcommand.
    // This maps  `dotnet-version [<input>] [options]`
    //        to  `dotnet-version read [<input>] [options]`
    effectiveArgs = ["read", .. args];
}

return await rootCommand.Parse(effectiveArgs).InvokeAsync();

