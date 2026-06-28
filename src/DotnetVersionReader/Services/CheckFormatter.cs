using System.Text.Encodings.Web;
using System.Text.Json;
using ConsoleTables;
using DotnetVersion.Models;

namespace DotnetVersion.Services;

/// <summary>
/// Formats a list of <see cref="CheckResult"/> objects for console output.
/// </summary>
public sealed class CheckFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Formats the check results according to the chosen <paramref name="format"/>.</summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="format"/> is <see cref="OutputFormat.Version"/> and
    /// <paramref name="results"/> contains more than one entry.
    /// </exception>
    public string Format(IReadOnlyList<CheckResult> results, OutputFormat format)
        => format switch
        {
            OutputFormat.Json    => FormatJson(results),
            OutputFormat.Table   => FormatTable(results),
            OutputFormat.Version => FormatVersion(results),
            _                    => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    // -------------------------------------------------------------------------

    private static string FormatJson(IReadOnlyList<CheckResult> results)
    {
        var items = results.Select(r => new
        {
            r.Name,
            r.FilePath,
            r.HeadVersion,
            r.BaseVersion,
            Status = r.Status.ToString()
        }).ToList();

        return JsonSerializer.Serialize(items, JsonOptions);
    }

    private static string FormatTable(IReadOnlyList<CheckResult> results)
    {
        if (results.Count == 0)
            return string.Empty;

        var table = new ConsoleTable("Name", "HeadVersion", "BaseVersion", "Status");
        foreach (var r in results)
            table.AddRow(r.Name, r.HeadVersion, r.BaseVersion ?? "(new)", r.Status.ToString());

        return table.ToMarkDownString().TrimEnd();
    }

    private static string FormatVersion(IReadOnlyList<CheckResult> results)
    {
        if (results.Count > 1)
        {
            var names = string.Join(", ", results.Select(r => r.Name));
            throw new InvalidOperationException(
                $"Output format 'version' requires exactly one project, but {results.Count} were found: {names}");
        }

        if (results.Count == 0)
            return string.Empty;

        var single = results[0];
        if (single.Status == CheckResultStatus.BumpRequired)
            throw new InvalidOperationException(
                $"Project '{single.Name}' requires a version bump. " +
                $"Current version '{single.HeadVersion}' is the same as on the base branch.");

        return single.HeadVersion;
    }
}
