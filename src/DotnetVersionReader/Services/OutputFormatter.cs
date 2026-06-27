using System.Text.Encodings.Web;
using System.Text.Json;
using ConsoleTables;
using DotnetVersion.Models;

namespace DotnetVersion.Services;

/// <summary>
/// Formats a list of <see cref="ProjectVersionInfo"/> objects for console output.
/// </summary>
public sealed class OutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Formats the results according to the chosen <paramref name="format"/>.</summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="format"/> is <see cref="OutputFormat.Version"/> and
    /// <paramref name="results"/> contains more than one entry.
    /// </exception>
    public string Format(IReadOnlyList<ProjectVersionInfo> results, OutputFormat format)
        => format switch
        {
            OutputFormat.Json    => FormatJson(results),
            OutputFormat.Table   => FormatTable(results),
            OutputFormat.Version => FormatVersion(results),
            _                    => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    // -------------------------------------------------------------------------

    private static string FormatJson(IReadOnlyList<ProjectVersionInfo> results)
    {
        var items = results.Select(r => new
        {
            r.Name,
            Version = r.ResolvedVersion,
            r.Major,
            r.Minor,
            r.Patch,
            Suffix = r.ResolvedSuffix
        }).ToList();

        return JsonSerializer.Serialize(items, JsonOptions);
    }

    private static string FormatTable(IReadOnlyList<ProjectVersionInfo> results)
    {
        if (results.Count == 0)
            return string.Empty;

        var table = new ConsoleTable("Name", "Version", "Major", "Minor", "Patch", "Suffix");
        foreach (var r in results)
            table.AddRow(r.Name, r.ResolvedVersion, r.Major, r.Minor, r.Patch, r.ResolvedSuffix);

        return table.ToMarkDownString().TrimEnd();
    }

    private static string FormatVersion(IReadOnlyList<ProjectVersionInfo> results)
    {
        if (results.Count > 1)
        {
            var names = string.Join(", ", results.Select(r => r.Name));
            throw new InvalidOperationException(
                $"Output format 'version' requires exactly one project, but {results.Count} were found: {names}");
        }

        return results.Count == 0 ? string.Empty : results[0].ResolvedVersion;
    }
}

