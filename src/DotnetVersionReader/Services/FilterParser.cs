using System.Text.RegularExpressions;

namespace DotnetVersion.Services;

/// <summary>
/// Parses filter strings of the form <c>XmlNode=Value</c> where <c>Value</c> may be a regex.
/// </summary>
public sealed class FilterParser
{
    /// <summary>
    /// Parses a collection of raw filter strings and returns typed tuples.
    /// </summary>
    /// <param name="rawFilters">
    /// Each string must be in the form <c>XmlElementName=Regex</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when a filter string does not contain an <c>=</c> separator.
    /// </exception>
    public IReadOnlyList<(string Element, Regex Pattern)> Parse(IEnumerable<string> rawFilters)
    {
        var result = new List<(string, Regex)>();

        foreach (var raw in rawFilters)
        {
            var idx = raw.IndexOf('=');
            if (idx <= 0)
                throw new ArgumentException($"Invalid filter '{raw}'. Expected format: XmlNode=Value", nameof(rawFilters));

            var element = raw[..idx].Trim();
            var patternStr = raw[(idx + 1)..];

            Regex pattern;
            try
            {
                pattern = new Regex(patternStr, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Invalid regex in filter '{raw}': {ex.Message}", nameof(rawFilters), ex);
            }

            result.Add((element, pattern));
        }

        return result;
    }
}

