using DotnetVersion.Services;
using DotnetVersion.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotnetVersion.Tests.Services;

/// <summary>
/// Integration tests for <see cref="GitService"/> that spin up a real (temporary) git repository.
/// Each test method creates an isolated repo, so tests are fully independent.
/// </summary>
[TestClass]
public sealed class GitServiceTests
{
    private GitService _svc = null!;
    private List<string> _reposToDelete = null!;

    [TestInitialize]
    public void Setup()
    {
        _svc             = new GitService(new CsprojParser());
        _reposToDelete   = [];
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var repo in _reposToDelete)
        {
            try
            {
                // git objects are read-only on Windows – force-delete
                SetNormalAttributes(repo);
                Directory.Delete(repo, recursive: true);
            }
            catch { /* best-effort */ }
        }
    }

    // -------------------------------------------------------------------------
    // GetRepositoryRoot
    // -------------------------------------------------------------------------

    [TestMethod]
    public void GetRepositoryRoot_InsideRepo_ReturnsCorrectRoot()
    {
        var repo    = CreateInitializedRepo([]);
        var subDir  = Path.Combine(repo, "src", "Lib");
        Directory.CreateDirectory(subDir);

        var root = _svc.GetRepositoryRoot(subDir);

        Assert.AreEqual(
            DependencyGraphService.NormalizePath(repo),
            DependencyGraphService.NormalizePath(root),
            ignoreCase: true);
    }

    // -------------------------------------------------------------------------
    // GetChangedFiles
    // -------------------------------------------------------------------------

    [TestMethod]
    public void GetChangedFiles_ModifiedFile_ReturnsAbsolutePath()
    {
        var repo = CreateInitializedRepo([("README.md", "initial")]);

        // Make a change on a new branch
        RunGit(["checkout", "-b", "feature"], repo);
        File.WriteAllText(Path.Combine(repo, "README.md"), "updated");
        RunGit(["add", "."], repo);
        RunGit(["commit", "-m", "update readme"], repo);

        var changed = _svc.GetChangedFiles("main", "HEAD", repo);

        Assert.AreEqual(1, changed.Count);
        Assert.IsTrue(changed[0].EndsWith("README.md", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(Path.IsPathRooted(changed[0]), "Path should be absolute");
    }

    [TestMethod]
    public void GetChangedFiles_NewFile_IsIncluded()
    {
        var repo = CreateInitializedRepo([("existing.txt", "hello")]);

        RunGit(["checkout", "-b", "feature"], repo);
        File.WriteAllText(Path.Combine(repo, "newfile.txt"), "new content");
        RunGit(["add", "."], repo);
        RunGit(["commit", "-m", "add new file"], repo);

        var changed = _svc.GetChangedFiles("main", "HEAD", repo);

        Assert.IsTrue(changed.Any(f => f.EndsWith("newfile.txt", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void GetChangedFiles_NoChanges_ReturnsEmpty()
    {
        var repo = CreateInitializedRepo([("file.txt", "content")]);

        RunGit(["checkout", "-b", "feature"], repo);
        // No changes on branch

        var changed = _svc.GetChangedFiles("main", "HEAD", repo);

        Assert.AreEqual(0, changed.Count);
    }

    // -------------------------------------------------------------------------
    // GetChangedFiles – working tree (unstaged / staged) changes
    // Bug: previously only committed diffs were returned; local modifications
    // were silently ignored, causing `check` to return [] on the same branch.
    // -------------------------------------------------------------------------

    [TestMethod]
    public void GetChangedFiles_UnstagedModification_IsIncluded()
    {
        // Start on main, modify a file without committing
        var repo = CreateInitializedRepo([("src/MyLib.cs", "original")]);
        File.WriteAllText(Path.Combine(repo, "src", "MyLib.cs"), "modified");

        var changed = _svc.GetChangedFiles("main", "HEAD", repo);

        Assert.IsTrue(
            changed.Any(f => f.EndsWith("MyLib.cs", StringComparison.OrdinalIgnoreCase)),
            "Unstaged modified file must be reported as changed");
    }

    [TestMethod]
    public void GetChangedFiles_StagedModification_IsIncluded()
    {
        var repo = CreateInitializedRepo([("src/MyLib.cs", "original")]);
        File.WriteAllText(Path.Combine(repo, "src", "MyLib.cs"), "modified");
        RunGit(["add", "."], repo);
        // NOT committed – only staged

        var changed = _svc.GetChangedFiles("main", "HEAD", repo);

        Assert.IsTrue(
            changed.Any(f => f.EndsWith("MyLib.cs", StringComparison.OrdinalIgnoreCase)),
            "Staged (but not committed) modified file must be reported as changed");
    }

    [TestMethod]
    public void GetChangedFiles_UntrackedNewFile_IsIncluded()
    {
        var repo = CreateInitializedRepo([("existing.txt", "hello")]);
        File.WriteAllText(Path.Combine(repo, "newfile.txt"), "new content");
        // NOT staged, NOT committed

        var changed = _svc.GetChangedFiles("main", "HEAD", repo);

        Assert.IsTrue(
            changed.Any(f => f.EndsWith("newfile.txt", StringComparison.OrdinalIgnoreCase)),
            "Untracked new file must be reported as changed");
    }

    [TestMethod]
    public void GetChangedFiles_CsprojVersionBumpedUncommitted_IsIncluded()
    {
        // Simulate the exact user scenario: version bumped in .csproj but not committed
        var repo       = CreateInitializedRepo([("MyLib/MyLib.csproj", CsprojFixtures.Library("1.0.0"))]);
        var csprojPath = Path.Combine(repo, "MyLib", "MyLib.csproj");

        // Bump version locally without committing (mirrors real workflow)
        File.WriteAllText(csprojPath, CsprojFixtures.Library("1.1.0"));

        var changed = _svc.GetChangedFiles("main", "HEAD", repo);

        Assert.IsTrue(
            changed.Any(f => string.Equals(f, csprojPath, StringComparison.OrdinalIgnoreCase)),
            "Uncommitted .csproj version bump must be reported as changed");
    }

    // -------------------------------------------------------------------------
    // GetVersionAtRef
    // -------------------------------------------------------------------------

    [TestMethod]
    public void GetVersionAtRef_FileExistsOnRef_ReturnsVersion()
    {
        var csprojContent = CsprojFixtures.Library("2.3.4");
        var repo          = CreateInitializedRepo([("MyLib/MyLib.csproj", csprojContent)]);

        var csprojPath = Path.Combine(repo, "MyLib", "MyLib.csproj");
        var version    = _svc.GetVersionAtRef("main", csprojPath, repo);

        Assert.AreEqual("2.3.4", version);
    }

    [TestMethod]
    public void GetVersionAtRef_FileNotOnRef_ReturnsNull()
    {
        var repo = CreateInitializedRepo([("README.md", "hi")]);

        RunGit(["checkout", "-b", "feature"], repo);
        var csprojPath = Path.Combine(repo, "NewProject", "NewProject.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(csprojPath)!);
        File.WriteAllText(csprojPath, CsprojFixtures.Library("1.0.0"));
        RunGit(["add", "."], repo);
        RunGit(["commit", "-m", "add new project"], repo);

        // The .csproj does not exist on 'main'
        var version = _svc.GetVersionAtRef("main", csprojPath, repo);

        Assert.IsNull(version);
    }

    [TestMethod]
    public void GetVersionAtRef_VersionChangedOnBranch_ReturnsBaseVersion()
    {
        var repo       = CreateInitializedRepo([("MyLib/MyLib.csproj", CsprojFixtures.Library("1.0.0"))]);
        var csprojPath = Path.Combine(repo, "MyLib", "MyLib.csproj");

        RunGit(["checkout", "-b", "feature"], repo);
        File.WriteAllText(csprojPath, CsprojFixtures.Library("2.0.0"));
        RunGit(["add", "."], repo);
        RunGit(["commit", "-m", "bump version"], repo);

        // GetVersionAtRef should return the version on 'main', not on 'feature'
        var version = _svc.GetVersionAtRef("main", csprojPath, repo);

        Assert.AreEqual("1.0.0", version);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a temporary git repository, commits the given files to 'main', and returns
    /// the path to the repository root.
    /// </summary>
    private string CreateInitializedRepo(IEnumerable<(string RelativePath, string Content)> files)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"git-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _reposToDelete.Add(dir);

        RunGit(["init", "-b", "main"], dir);
        RunGit(["config", "user.email", "test@test.com"], dir);
        RunGit(["config", "user.name", "Test User"], dir);

        // Create an initial commit even if no files are given, so HEAD and 'main' exist
        var placeholderPath = Path.Combine(dir, ".gitkeep");
        File.WriteAllText(placeholderPath, "");

        foreach (var (relativePath, content) in files)
        {
            var fullPath = Path.Combine(dir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }

        RunGit(["add", "."], dir);
        RunGit(["commit", "-m", "initial commit"], dir);

        return dir;
    }

    private static void RunGit(IReadOnlyList<string> args, string workingDir)
        => GitService.RunGit(args, workingDir);

    private static void SetNormalAttributes(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); } catch { /* best-effort */ }
        }
    }
}
