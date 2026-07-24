namespace DotnetVersion.Models;

/// <summary>
/// The kind of reference a <see cref="DependencyChange"/> describes.
/// </summary>
public enum DependencyKind
{
    /// <summary>A NuGet <c>&lt;PackageReference&gt;</c>.</summary>
    Package,

    /// <summary>An MSBuild <c>&lt;ProjectReference&gt;</c>.</summary>
    Project
}
