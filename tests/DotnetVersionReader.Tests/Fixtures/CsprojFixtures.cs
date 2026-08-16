namespace DotnetVersionReader.Tests.Fixtures;

/// <summary>
/// In-memory .csproj XML strings used by the unit tests.
/// </summary>
public static class CsprojFixtures
{
    public const string WithVersionOnly = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <Version>3.2.1</Version>
          </PropertyGroup>
        </Project>
        """;

    public const string WithVersionPrefixOnly = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <VersionPrefix>2.0.0</VersionPrefix>
          </PropertyGroup>
        </Project>
        """;

    public const string WithVersionSuffixOnly = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <VersionSuffix>beta.1</VersionSuffix>
          </PropertyGroup>
        </Project>
        """;

    public const string WithVersionPrefixAndSuffix = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <VersionPrefix>1.2.3</VersionPrefix>
            <VersionSuffix>rc.2</VersionSuffix>
          </PropertyGroup>
        </Project>
        """;

    public const string WithVersionAndPrefixSuffix = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <Version>9.9.9</Version>
            <VersionPrefix>1.0.0</VersionPrefix>
            <VersionSuffix>ignored</VersionSuffix>
          </PropertyGroup>
        </Project>
        """;

    public const string WithNoVersion = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net9.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    public const string WithGeneratePackageOnBuildTrue = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <Version>1.0.0</Version>
            <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
          </PropertyGroup>
        </Project>
        """;

    public const string WithGeneratePackageOnBuildFalse = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <Version>2.0.0</Version>
            <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
          </PropertyGroup>
        </Project>
        """;

    public const string WithDeeplyNestedProperty = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <Version>5.0.0</Version>
          </PropertyGroup>
          <PropertyGroup Condition="'$(Configuration)' == 'Release'">
            <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
          </PropertyGroup>
        </Project>
        """;

    public const string WithTargetFrameworkNet9 = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net9.0</TargetFramework>
            <Version>4.0.0</Version>
          </PropertyGroup>
        </Project>
        """;

    // -------------------------------------------------------------------------
    // Fixtures for dependency-graph / check tests
    // -------------------------------------------------------------------------

    /// <summary>Creates a .csproj that has a single &lt;ProjectReference&gt; to <paramref name="refPath"/>.</summary>
    public static string WithProjectReference(string refPath, string version = "1.0.0") => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <Version>{version}</Version>
          </PropertyGroup>
          <ItemGroup>
            <ProjectReference Include="{refPath.Replace('\\', '/')}" />
          </ItemGroup>
        </Project>
        """;

    /// <summary>A simple library project with a configurable version.</summary>
    public static string Library(string version = "1.0.0") => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <Version>{version}</Version>
          </PropertyGroup>
        </Project>
        """;

    // -------------------------------------------------------------------------
    // Fixtures for PackageReference / ProjectReference / CPM (Directory.Packages.props)
    // -------------------------------------------------------------------------

    /// <summary>A project with a single &lt;PackageReference&gt; whose version is declared inline.</summary>
    public static string WithPackageReference(string packageId, string packageVersion, string projectVersion = "1.0.0") => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <Version>{projectVersion}</Version>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="{packageId}" Version="{packageVersion}" />
          </ItemGroup>
        </Project>
        """;

    /// <summary>A project with a single &lt;PackageReference&gt; that has no version attribute (relies on CPM).</summary>
    public static string WithCentrallyManagedPackageReference(string packageId, string projectVersion = "1.0.0") => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <Version>{projectVersion}</Version>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="{packageId}" />
          </ItemGroup>
        </Project>
        """;

    /// <summary>A project with multiple &lt;PackageReference&gt; and &lt;ProjectReference&gt; entries.</summary>
    public static string WithDependencies(
        string projectVersion,
        IEnumerable<(string Id, string? Version)> packages,
        IEnumerable<string> projectRefs) => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <Version>{projectVersion}</Version>
          </PropertyGroup>
          <ItemGroup>
        {string.Join(Environment.NewLine, packages.Select(p =>
            p.Version is null
                ? $"    <PackageReference Include=\"{p.Id}\" />"
                : $"    <PackageReference Include=\"{p.Id}\" Version=\"{p.Version}\" />"))}
        {string.Join(Environment.NewLine, projectRefs.Select(r => $"    <ProjectReference Include=\"{r.Replace('\\', '/')}\" />"))}
          </ItemGroup>
        </Project>
        """;

    /// <summary>A minimal <c>Directory.Packages.props</c> content declaring the given package versions.</summary>
    public static string DirectoryPackagesProps(IEnumerable<(string Id, string Version)> packages) => $"""
        <Project>
          <PropertyGroup>
            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
          </PropertyGroup>
          <ItemGroup>
        {string.Join(Environment.NewLine, packages.Select(p => $"    <PackageVersion Include=\"{p.Id}\" Version=\"{p.Version}\" />"))}
          </ItemGroup>
        </Project>
        """;
}

