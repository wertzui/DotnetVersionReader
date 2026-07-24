using System.Text.Encodings.Web;
using System.Text.Json;
using ConsoleTables;
using DotnetVersion.Models;

namespace DotnetVersion.Services;

// ---------------------------------------------------------------------------
// Options that describe how to format a specific result type T.
// ---------------------------------------------------------------------------

/// <summary>
/// Describes how to format a list of <typeparamref name="T"/> items.
/// </summary>
/// <typeparam name="T">The result type (e.g. <see cref="ProjectVersionInfo"/> or <see cref="CheckResult"/>).</typeparam>
/// <param name="ToJsonRow">Projects one item to an anonymous/serialisable object for JSON output.</param>
/// <param name="TableColumns">Column headers for the table output.</param>
/// <param name="ToTableRow">Projects one item to an object array matching <paramref name="TableColumns"/>.</param>
/// <param name="GetVersion">Returns the version string for a single item (used by <see cref="OutputFormat.Version"/>).</param>
/// <param name="ValidateVersionItem">
/// Optional extra validation called before returning the version string.
/// Throw <see cref="InvalidOperationException"/> to signal an error.
/// </param>
public sealed record FormatterOptions<T>(
    Func<T, object>        ToJsonRow,
    string[]               TableColumns,
    Func<T, object?[]>     ToTableRow,
    Func<T, string>        GetVersion,
    Func<T, string>        GetName,
    Action<T>?             ValidateVersionItem = null);

// ---------------------------------------------------------------------------
// Formatter
// ---------------------------------------------------------------------------

/// <summary>
/// Formats output for both the <c>read</c> command (<see cref="ProjectVersionInfo"/> list)
/// and the <c>check</c> command (<see cref="CheckResult"/> list).
/// </summary>
public sealed class Formatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // =========================================================================
    // Pre-built options for the two known result types
    // =========================================================================

    /// <summary>Options for formatting <see cref="ProjectVersionInfo"/> results (the <c>read</c> command).</summary>
    public static readonly FormatterOptions<ProjectVersionInfo> ReadOptions = new(
        ToJsonRow: r => new
        {
            r.Name,
            Version = r.ResolvedVersion,
            r.Major,
            r.Minor,
            r.Patch,
            Suffix = r.ResolvedSuffix
        },
        TableColumns: ["Name", "Version", "Major", "Minor", "Patch", "Suffix"],
        ToTableRow: r => [r.Name, r.ResolvedVersion, r.Major, r.Minor, r.Patch, r.ResolvedSuffix],
        GetVersion: r => r.ResolvedVersion,
        GetName: r => r.Name);

    /// <summary>Options for formatting <see cref="DiffResult"/> results (the <c>diff</c> command).</summary>
    public static readonly FormatterOptions<DiffResult> DiffOptions = new(
        ToJsonRow: r => new
        {
            r.Name,
            r.FilePath,
            r.HeadVersion,
            r.BaseVersion,
            Status = r.Status.ToString(),
            DependencyChanges = r.DependencyChanges.Select(c => new
            {
                Kind = c.Kind.ToString(),
                c.Name,
                c.BaseVersion,
                c.HeadVersion,
                BumpType = c.BumpType.ToString()
            }),
            r.SuggestedVersionPrefix,
            r.SuggestedVersionSuffix,
            r.SuggestedVersion
        },
        TableColumns: ["Name", "HeadVersion", "BaseVersion", "Status", "SuggestedVersion"],
        ToTableRow: r => [r.Name, r.HeadVersion, r.BaseVersion ?? "(new)", r.Status.ToString(), r.SuggestedVersion ?? ""],
        GetVersion: r => r.SuggestedVersion ?? r.HeadVersion,
        GetName: r => r.Name);

    /// <summary>Options for formatting <see cref="CheckResult"/> results (the <c>check</c> command).</summary>
    public static readonly FormatterOptions<CheckResult> CheckOptions = new(
        ToJsonRow: r => new
        {
            r.Name,
            r.FilePath,
            r.HeadVersion,
            r.BaseVersion,
            Status = r.Status.ToString()
        },
        TableColumns: ["Name", "HeadVersion", "BaseVersion", "Status"],
        ToTableRow: r => [r.Name, r.HeadVersion, r.BaseVersion ?? "(new)", r.Status.ToString()],
        GetVersion: r => r.HeadVersion,
        GetName: r => r.Name,
        ValidateVersionItem: r =>
        {
            if (r.Status == CheckResultStatus.BumpRequired)
                throw new InvalidOperationException(
                    $"Project '{r.Name}' requires a version bump. " +
                    $"Current version '{r.HeadVersion}' is the same as on the base branch.");
        });

    // =========================================================================
    // Generic core – one method per output style
    // =========================================================================

    /// <summary>
    /// Formats <paramref name="results"/> using the supplied <paramref name="options"/>
    /// and <paramref name="format"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="format"/> is <see cref="OutputFormat.Version"/> and
    /// <paramref name="results"/> contains more than one entry, or
    /// <see cref="FormatterOptions{T}.ValidateVersionItem"/> throws.
    /// </exception>
    public string Format<T>(IReadOnlyList<T> results, OutputFormat format, FormatterOptions<T> options)
        => format switch
        {
            OutputFormat.Json    => FormatJson(results, options),
            OutputFormat.Table   => FormatTable(results, options),
            OutputFormat.Version => FormatVersion(results, options),
            OutputFormat.List    => FormatList(results, options),
            _                    => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    private static string FormatJson<T>(IReadOnlyList<T> results, FormatterOptions<T> options)
    {
        var items = results.Select(options.ToJsonRow).ToList();
        return JsonSerializer.Serialize(items, JsonOptions);
    }

    private static string FormatTable<T>(IReadOnlyList<T> results, FormatterOptions<T> options)
    {
        if (results.Count == 0)
            return string.Empty;

        var table = new ConsoleTable(options.TableColumns);
        foreach (var r in results)
            table.AddRow(options.ToTableRow(r));

        return table.ToMarkDownString().TrimEnd();
    }

    private static string FormatList<T>(IReadOnlyList<T> results, FormatterOptions<T> options)
    {
        if (results.Count == 0)
            return string.Empty;

        var lines = results.Select(r => $"{options.GetName(r)} {options.GetVersion(r)}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatVersion<T>(IReadOnlyList<T> results, FormatterOptions<T> options)
    {
        if (results.Count > 1)
        {
            // Best-effort: use Name property via options.ToJsonRow is too indirect;
            // instead reflect on a well-known "Name" property or fall back to index.
            // Since both result types have a Name property we can use it generically
            // through a simple runtime check.
            var names = string.Join(", ", results.Select(options.GetName));
            throw new InvalidOperationException(
                $"Output format 'version' requires exactly one project, but {results.Count} were found: {names}");
        }

        if (results.Count == 0)
            return string.Empty;

        var single = results[0];
        options.ValidateVersionItem?.Invoke(single);
        return options.GetVersion(single);
    }
}
