namespace DotnetVersion.Models;

/// <summary>
/// A single <c>&lt;PackageReference&gt;</c> entry as declared in a .csproj file.
/// </summary>
public sealed record PackageReferenceEntry
{
    /// <summary>The package id (the <c>Include</c> / <c>Update</c> attribute value).</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The fully-resolved version: either the value declared directly on the
    /// <c>&lt;PackageReference&gt;</c> element (via the <c>Version</c> attribute or a nested
    /// <c>&lt;Version&gt;</c> element), or, if not declared inline, the version looked up by
    /// package id in a <c>Directory.Packages.props</c> file (NuGet Central Package
    /// Management). <see langword="null"/> when the version cannot be resolved either way.
    /// </summary>
    public string? Version { get; init; }
}
