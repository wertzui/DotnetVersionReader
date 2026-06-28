using System.Diagnostics;
using DotnetVersion.Models;

namespace DotnetVersion.Services;

/// <summary>
/// Provides git-based operations needed by the <c>check</c> command:
/// listing changed files between two refs and reading a .csproj version at a specific ref.
/// </summary>
public sealed class GitService
{
    private readonly CsprojParser _parser;

    /// <param name="parser">
    /// The parser used to extract version information from .csproj content retrieved from git.
    /// </param>
    public GitService(CsprojParser parser)
    {
        _parser = parser;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the set of files that differ between <paramref name="baseRef"/> and the
    /// current state of the repository (committed, staged, unstaged, and untracked), as
    /// absolute paths.
    /// </summary>
    /// <remarks>
    /// The method unions four sources so that it works both in a PR context (committed
    /// changes on a feature branch) and in a local workflow (modifications not yet
    /// committed):
    /// <list type="number">
    ///   <item>Committed diff: <c>git diff --name-only <paramref name="baseRef"/>...<paramref name="headRef"/></c></item>
    ///   <item>Staged changes: <c>git diff --name-only --cached <paramref name="baseRef"/></c></item>
    ///   <item>Unstaged tracked changes: <c>git diff --name-only <paramref name="baseRef"/></c></item>
    ///   <item>Untracked new files: <c>git ls-files --others --exclude-standard</c></item>
    /// </list>
    /// </remarks>
    /// <param name="baseRef">The base git ref (e.g. <c>origin/main</c>).</param>
    /// <param name="headRef">The head git ref (e.g. <c>HEAD</c>). Defaults to <c>HEAD</c>.</param>
    /// <param name="repositoryRoot">
    /// The repository root directory. If <see langword="null"/>, the current working
    /// directory is used.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when git is unavailable or the command fails.
    /// </exception>
    public IReadOnlyList<string> GetChangedFiles(
        string baseRef,
        string headRef,
        string? repositoryRoot = null)
    {
        var workingDir = repositoryRoot ?? Directory.GetCurrentDirectory();
        var repoRoot   = GetRepositoryRoot(workingDir);

        var relativeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Committed differences between base and head
        CollectGitOutput(
            ["diff", "--name-only", $"{baseRef}...{headRef}"],
            repoRoot, relativeFiles);

        // 2. Staged (indexed) changes not yet committed
        CollectGitOutput(
            ["diff", "--name-only", "--cached", baseRef],
            repoRoot, relativeFiles);

        // 3. Unstaged tracked changes in the working tree
        CollectGitOutput(
            ["diff", "--name-only", baseRef],
            repoRoot, relativeFiles);

        // 4. Untracked new files (not in .gitignore)
        CollectGitOutput(
            ["ls-files", "--others", "--exclude-standard"],
            repoRoot, relativeFiles);

        return relativeFiles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(relative => Path.GetFullPath(Path.Combine(repoRoot, relative)))
            .ToList();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs a git command and adds each non-empty output line to <paramref name="target"/>.
    /// Failures are silently swallowed (e.g. when a ref doesn't exist yet).
    /// </summary>
    private static void CollectGitOutput(
        IReadOnlyList<string> arguments,
        string workingDirectory,
        HashSet<string> target)
    {
        string output;
        try
        {
            output = RunGit(arguments, workingDirectory);
        }
        catch
        {
            return; // non-fatal: ref may not exist, git may not support the flag, etc.
        }

        foreach (var line in output.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                target.Add(trimmed);
        }
    }

    /// <summary>
    /// Reads the resolved version of the project at <paramref name="csprojPath"/>
    /// as it exists at <paramref name="gitRef"/>.
    /// Returns <see langword="null"/> when the file does not exist at that ref.
    /// </summary>
    /// <param name="gitRef">The git ref to read from (e.g. <c>origin/main</c>).</param>
    /// <param name="csprojPath">Absolute path to the .csproj file in the working tree.</param>
    /// <param name="repositoryRoot">
    /// The repository root directory. If <see langword="null"/>, detected automatically.
    /// </param>
    public string? GetVersionAtRef(
        string gitRef,
        string csprojPath,
        string? repositoryRoot = null)
    {
        var workingDir = repositoryRoot ?? Path.GetDirectoryName(csprojPath) ?? Directory.GetCurrentDirectory();
        var repoRoot   = GetRepositoryRoot(workingDir);

        // Convert absolute path to a path relative to the repo root, using forward slashes
        // (git always uses forward slashes in object paths regardless of OS)
        var relative = Path.GetRelativePath(repoRoot, csprojPath)
            .Replace('\\', '/');

        string content;
        try
        {
            content = RunGit(["show", $"{gitRef}:{relative}"], repoRoot);
        }
        catch (InvalidOperationException)
        {
            // File does not exist at that ref
            return null;
        }

        var info = _parser.ParseFromString(content, csprojPath);
        return info?.ResolvedVersion;
    }

    /// <summary>
    /// Returns the absolute path to the root of the git repository that contains
    /// <paramref name="workingDir"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="workingDir"/> is not inside a git repository.
    /// </exception>
    public string GetRepositoryRoot(string workingDir)
    {
        var root = RunGit(["rev-parse", "--show-toplevel"], workingDir).Trim();
        // On Windows git may return forward-slash paths – normalise
        return Path.GetFullPath(root.Replace('/', Path.DirectorySeparatorChar));
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs a git command with the given <paramref name="arguments"/> in
    /// <paramref name="workingDirectory"/> and returns standard output.
    /// Throws <see cref="InvalidOperationException"/> on non-zero exit codes.
    /// </summary>
    public static string RunGit(IReadOnlyList<string> arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory       = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };

        process.Start();

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var cmd = $"git {string.Join(" ", arguments)}";
            throw new InvalidOperationException(
                $"'{cmd}' exited with code {process.ExitCode}. stderr: {stderr.Trim()}");
        }

        return stdout;
    }
}
