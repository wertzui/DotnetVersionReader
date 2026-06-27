namespace DotnetVersion.Models;

/// <summary>Supported output formats.</summary>
public enum OutputFormat
{
    /// <summary>JSON array (default).</summary>
    Json,

    /// <summary>Plain text table.</summary>
    Table,

    /// <summary>
    /// Outputs the bare version string. Fails if more than one project is found.
    /// </summary>
    Version
}

