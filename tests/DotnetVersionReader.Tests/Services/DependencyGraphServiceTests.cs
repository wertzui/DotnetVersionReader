using DotnetVersion.Services;
using DotnetVersion.Tests.Fixtures;
using DotnetVersion.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersion.Tests.Services;

[TestClass]
public sealed class DependencyGraphServiceTests
{
    private TempFileHelper _tmp = null!;
    private DependencyGraphService _svc = null!;

    [TestInitialize]
    public void Setup()
    {
        _tmp = new TempFileHelper();
        _svc = new DependencyGraphService();
    }

    [TestCleanup]
    public void Cleanup() => _tmp.Dispose();

    // -------------------------------------------------------------------------
    // Build – basic graph construction
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Build_SingleProject_CreatesOneNode()
    {
        var (_, projects) = _tmp.CreateProjectTree([("Lib", CsprojFixtures.Library())]);
        var graph = _svc.Build([projects["Lib"].CsprojPath]);

        Assert.AreEqual(1, graph.Nodes.Count);
    }

    [TestMethod]
    public void Build_ProjectWithReference_CreatesTwoNodes()
    {
        var (_, projects) = _tmp.CreateProjectTree([
            ("Core", CsprojFixtures.Library()),
            ("App",  CsprojFixtures.Library()) // will be rewritten with reference below
        ]);

        // Rewrite App.csproj to reference Core.csproj
        var appCsproj = projects["App"].CsprojPath;
        var coreCsproj = projects["Core"].CsprojPath;
        File.WriteAllText(appCsproj, CsprojFixtures.WithProjectReference(coreCsproj));

        var graph = _svc.Build([appCsproj]);

        // Graph should have discovered Core transitively
        Assert.AreEqual(2, graph.Nodes.Count);
    }

    [TestMethod]
    public void Build_ProjectWithReference_SetsReverseDependency()
    {
        var (_, projects) = _tmp.CreateProjectTree([
            ("Core", CsprojFixtures.Library()),
            ("App",  CsprojFixtures.Library())
        ]);

        var appCsproj  = projects["App"].CsprojPath;
        var coreCsproj = projects["Core"].CsprojPath;
        File.WriteAllText(appCsproj, CsprojFixtures.WithProjectReference(coreCsproj));

        var graph = _svc.Build([appCsproj]);

        Assert.IsTrue(graph.ReverseDependencies.ContainsKey(
            DependencyGraphService.NormalizePath(coreCsproj)));

        var dependents = graph.ReverseDependencies[DependencyGraphService.NormalizePath(coreCsproj)];
        Assert.AreEqual(1, dependents.Count);
        Assert.IsTrue(dependents.Any(d =>
            string.Equals(d, DependencyGraphService.NormalizePath(appCsproj), StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Build_OwnedFiles_IncludesFilesInProjectDirectory()
    {
        var (_, projects) = _tmp.CreateProjectTree([("Lib", CsprojFixtures.Library())]);
        var (_, libDir)   = projects["Lib"];

        // Add a source file to the project directory
        var srcFile = _tmp.CreateFile(libDir, "Class1.cs", "public class Class1 {}");

        var graph = _svc.Build([projects["Lib"].CsprojPath]);
        var node  = graph.Nodes.Values.Single();

        Assert.IsTrue(node.OwnedFiles.Any(f =>
            string.Equals(f, DependencyGraphService.NormalizePath(srcFile), StringComparison.OrdinalIgnoreCase)),
            "OwnedFiles should include Class1.cs");
    }

    [TestMethod]
    public void Build_OwnedFiles_ExcludesBinAndObjDirectories()
    {
        var (_, projects) = _tmp.CreateProjectTree([("Lib", CsprojFixtures.Library())]);
        var (_, libDir)   = projects["Lib"];

        // Simulate bin/obj artifacts
        _tmp.CreateFile(libDir, Path.Combine("bin", "Release", "Lib.dll"), "");
        _tmp.CreateFile(libDir, Path.Combine("obj", "Lib.csproj.nuget.g.props"), "");

        var graph = _svc.Build([projects["Lib"].CsprojPath]);
        var node  = graph.Nodes.Values.Single();

        Assert.IsFalse(node.OwnedFiles.Any(f => f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") || f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")),
            "OwnedFiles must not include bin/ or obj/ artifacts");
    }

    // -------------------------------------------------------------------------
    // GetAffectedProjects
    // -------------------------------------------------------------------------

    [TestMethod]
    public void GetAffectedProjects_NoChanges_ReturnsEmpty()
    {
        var (_, projects) = _tmp.CreateProjectTree([("Lib", CsprojFixtures.Library())]);
        var graph         = _svc.Build([projects["Lib"].CsprojPath]);

        var affected = _svc.GetAffectedProjects([], graph);

        Assert.AreEqual(0, affected.Count);
    }

    [TestMethod]
    public void GetAffectedProjects_CsprojChanged_ReturnsProject()
    {
        var (_, projects) = _tmp.CreateProjectTree([("Lib", CsprojFixtures.Library())]);
        var csprojPath    = projects["Lib"].CsprojPath;
        var graph         = _svc.Build([csprojPath]);

        var affected = _svc.GetAffectedProjects([csprojPath], graph);

        Assert.AreEqual(1, affected.Count);
        Assert.AreEqual(DependencyGraphService.NormalizePath(csprojPath), affected[0].CsprojPath);
    }

    [TestMethod]
    public void GetAffectedProjects_SourceFileChanged_ReturnsOwningProject()
    {
        var (_, projects) = _tmp.CreateProjectTree([("Lib", CsprojFixtures.Library())]);
        var (_, libDir)   = projects["Lib"];
        var srcFile       = _tmp.CreateFile(libDir, "Service.cs", "");
        var graph         = _svc.Build([projects["Lib"].CsprojPath]);

        var affected = _svc.GetAffectedProjects([srcFile], graph);

        Assert.AreEqual(1, affected.Count);
    }

    [TestMethod]
    public void GetAffectedProjects_DependencyChanged_BubblesUpToConsumer()
    {
        var (_, projects) = _tmp.CreateProjectTree([
            ("Core", CsprojFixtures.Library()),
            ("App",  CsprojFixtures.Library())
        ]);

        var appCsproj  = projects["App"].CsprojPath;
        var coreCsproj = projects["Core"].CsprojPath;
        File.WriteAllText(appCsproj, CsprojFixtures.WithProjectReference(coreCsproj));

        var graph   = _svc.Build([appCsproj, coreCsproj]);

        // Simulate a source file change in Core
        var (_, coreDir) = projects["Core"];
        var coreFile = _tmp.CreateFile(coreDir, "CoreService.cs", "");

        var affected = _svc.GetAffectedProjects([coreFile], graph);

        // Both Core and App should be affected
        Assert.AreEqual(2, affected.Count);
        Assert.IsTrue(affected.Any(n => string.Equals(n.CsprojPath, DependencyGraphService.NormalizePath(coreCsproj), StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(affected.Any(n => string.Equals(n.CsprojPath, DependencyGraphService.NormalizePath(appCsproj), StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void GetAffectedProjects_UnrelatedFileChanged_ReturnsEmpty()
    {
        var (_, projects) = _tmp.CreateProjectTree([("Lib", CsprojFixtures.Library())]);
        var graph         = _svc.Build([projects["Lib"].CsprojPath]);

        // A file that is not under any project directory
        var unrelated = Path.Combine(Path.GetTempPath(), "unrelated.md");

        var affected = _svc.GetAffectedProjects([unrelated], graph);

        Assert.AreEqual(0, affected.Count);
    }

    [TestMethod]
    public void GetAffectedProjects_TransitiveChain_AffectsAllUpstream()
    {
        // Core ← Middle ← Top
        var (_, projects) = _tmp.CreateProjectTree([
            ("Core",   CsprojFixtures.Library()),
            ("Middle", CsprojFixtures.Library()),
            ("Top",    CsprojFixtures.Library())
        ]);

        var corePath   = projects["Core"].CsprojPath;
        var middlePath = projects["Middle"].CsprojPath;
        var topPath    = projects["Top"].CsprojPath;

        File.WriteAllText(middlePath, CsprojFixtures.WithProjectReference(corePath));
        File.WriteAllText(topPath,    CsprojFixtures.WithProjectReference(middlePath));

        var graph = _svc.Build([corePath, middlePath, topPath]);

        var (_, coreDir) = projects["Core"];
        var changedFile  = _tmp.CreateFile(coreDir, "Changed.cs", "");

        var affected = _svc.GetAffectedProjects([changedFile], graph);

        Assert.AreEqual(3, affected.Count, "All three projects should be affected");
    }
}
