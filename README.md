# DotnetVersionReader

![DotnetVersionReader logo](logo.png)

A .NET global tool for reading version information from `.csproj`, `.sln`, and `.slnx` files,
and for enforcing version bumps in pull requests.

[![Build, Test, Pack, Publish](https://github.com/wertzui/DotnetVersionReader/actions/workflows/build-test-pack-publish.yml/badge.svg)](https://github.com/wertzui/DotnetVersionReader/actions/workflows/build-test-pack-publish.yml)
[![NuGet](https://img.shields.io/nuget/v/DotnetVersionReader)](https://www.nuget.org/packages/DotnetVersionReader)

## Installation

```bash
dotnet tool install --global DotnetVersionReader
```

Or from a local build:

```bash
dotnet pack src/DotnetVersionReader -c Release
dotnet tool install --global DotnetVersionReader --version <version> --add-source ./src/DotnetVersionReader/bin/Release
```

---

## Commands

```bash
dotnet-version [command] [options]

Commands:
  read    Reads and displays version information from .csproj files. (default)
  check   Checks that every project whose source files have changed has had its version bumped.
```

---

### `dotnet-version read` — read versions (default)

Reads and displays version information. This is the **default command**: running
`dotnet-version` with no subcommand is equivalent to `dotnet-version read`.

```bash
# Both forms are equivalent:
dotnet-version          [--input <path>] [options]
dotnet-version read     [--input <path>] [options]
```

#### Options

| Option | Short | Description |
| -------- | ------- | ------------- |
| `--input` | `-i` | Path to a `.csproj`, `.sln`, `.slnx` file **or** a folder. Defaults to the current directory. |
| `--output` | `-o` | Output format: `json` (default), `table`, or `version` (single project only). |
| `--filter` | `-f` | Filter in the form `XmlNode=Value`. Value can be a regex. Repeatable. |
| `--schema` | | Print the JSON schema for `--output json` and exit. Defaults to `false`. |

#### Version resolution

The tool follows MSBuild semantics:

1. If `<Version>` is set, it is used as-is.
2. Otherwise the version is `<VersionPrefix>` (default `1.0.0`) optionally followed by `-<VersionSuffix>`.

#### Examples

```bash
# Current directory – JSON output (default, both forms are equivalent)
dotnet-version
dotnet-version read

# Specific solution file – table output
dotnet-version read --input MySolution.slnx --output table
dotnet-version read -i MySolution.slnx -o table

# Only projects that generate a NuGet package
dotnet-version read --filter "GeneratePackageOnBuild=true"

# Combine multiple filters (all must match)
dotnet-version read -i MySolution.slnx -f "TargetFramework=^net10\.0$" -f "GeneratePackageOnBuild=true"
```

#### Sample JSON output

```json
[
  {
    "Name": "MyLibrary",
    "Version": "2.1.0-rc.1",
    "Major": 2,
    "Minor": 1,
    "Patch": 0,
    "Suffix": "rc.1"
  },
  {
    "Name": "MyApp",
    "Version": "1.0.0",
    "Major": 1,
    "Minor": 0,
    "Patch": 0,
    "Suffix": null
  }
]
```

#### Sample table output

```text
| Name      | Version    | Major | Minor | Patch | Suffix |
|-----------|------------|-------|-------|-------|--------|
| MyLibrary | 2.1.0-rc.1 | 2     | 1     | 0     | rc.1   |
| MyApp     | 1.0.0      | 1     | 0     | 0     |        |
```

---

### `dotnet-version check` — enforce version bumps in PRs

Checks that every project whose source files have changed (compared to a base branch)
has had its version bumped. Designed to run as a CI gate on pull requests.

```bash
dotnet-version check [--base <ref>] [--input <path>] [--head <ref>] [--output <format>] [--filter <XmlNode=Value>]...

# Short aliases (--base defaults to origin/main):
dotnet-version check [-b <ref>] [-i <path>] [--head <ref>] [-o <format>] [-f <XmlNode=Value>]...
```

#### Options

| Option | Short | Required | Description |
| -------- | ------- | ---------- | ------------- |
| `--input` | `-i` | | Path to a `.csproj`, `.sln`, `.slnx` file **or** a folder. Defaults to the current directory. |
| `--base` | `-b` | | The git ref to compare against. Defaults to `origin/main`. |
| `--head` | | | The git ref for the current state. Defaults to `HEAD`. |
| `--output` | `-o` | | Output format: `json` (default), `table`, or `version` (single project only). |
| `--filter` | `-f` | | Filter in the form `XmlNode=Value`. Only matching projects are checked. Value can be a regex. Repeatable. |

#### Exit codes

| Code | Meaning |
| ------ | --------- |
| `0` | All affected projects have been version-bumped (or no relevant files changed). |
| `1` | At least one affected project has **not** been bumped — the check failed. |
| `2` | Usage or argument error (bad input path, git not found, etc.). |

#### How it works

1. Locates all `.csproj` files from `<input>`.
2. Builds a **dependency graph**: for each project, which files it owns and which other projects it references via `<ProjectReference>`.
3. Collects changed files by unioning: committed diff (`<base>...<head>`), staged changes, unstaged tracked changes, and untracked new files — so it works both in a PR context and with local uncommitted modifications.
4. Determines **affected projects** transitively: if a library changes, every project that depends on it (directly or indirectly) is also considered affected.
5. For each affected project, reads the version on `<base>` (via `git show`) and compares it to the version in the working tree.
6. Reports the result and exits with code `1` if any version was not bumped.

#### Examples

```bash
# Check current directory against origin/main (default, both are equivalent)
dotnet-version check
dotnet-version check --base origin/main

# Scope to a specific solution file
dotnet-version check --input MySolution.slnx --base origin/main
dotnet-version check -i MySolution.slnx -b origin/main

# Table output
dotnet-version check --input MySolution.slnx --base origin/main --output table

# Single project, bare version output (useful for scripts)
dotnet-version check --input src/MyLib/MyLib.csproj --base origin/main --output version

# Only check projects that produce a NuGet package
dotnet-version check --input MySolution.slnx --base origin/main --filter "GeneratePackageOnBuild=true"
```

#### Sample JSON output

```json
[
  {
    "Name": "MyLib",
    "FilePath": "src/MyLib/MyLib.csproj",
    "HeadVersion": "2.0.0",
    "BaseVersion": "1.0.0",
    "Status": "Ok"
  },
  {
    "Name": "MyApp",
    "FilePath": "src/MyApp/MyApp.csproj",
    "HeadVersion": "3.1.0",
    "BaseVersion": "3.1.0",
    "Status": "BumpRequired"
  }
]
```

Possible `Status` values:

| Value | Meaning |
| ------- | --------- |
| `Ok` | No relevant files changed, or the version was bumped. |
| `BumpRequired` | Files changed but the version is the same as on the base branch. |
| `NewProject` | The project did not exist on the base branch — no bump required. |

#### Sample table output

```
| Name  | HeadVersion | BaseVersion | Status       |
|-------|-------------|-------------|--------------|
| MyLib | 2.0.0       | 1.0.0       | Ok           |
| MyApp | 3.1.0       | 3.1.0       | BumpRequired |
```

#### GitHub Actions integration

```yaml
name: Check version bumps

on:
  pull_request:
    branches: [main]

jobs:
  check-versions:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0          # full history is required for git diff

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x

      - name: Install dotnet-version
        run: dotnet tool install --global DotnetVersionReader

      - name: Check version bumps
        run: dotnet-version check --input MySolution.slnx --base origin/main
```

> **Important:** `fetch-depth: 0` (or at least enough history to reach the base branch) is required; a shallow clone will cause `git diff` to fail.

---

## Development

```bash
# Restore & build
dotnet build DotnetVersionReader.slnx

# Run tests
dotnet test DotnetVersionReader.slnx

# Pack
dotnet pack DotnetVersionReader.slnx -c Release
```

## CI / CD

The repository uses two GitHub Actions workflows.

### `check-version-bump.yml` — PR gate

Runs on every pull request targeting `main`. Builds the tool from source and
runs `dotnet-version check` to ensure every NuGet-publishable project that
changed has had its version bumped.

```bash
dotnet-version check --input DotnetVersionReader.slnx --filter "GeneratePackageOnBuild=true"
```

The PR **must pass** this check before merging.

### `build-test-pack-publish.yml` — publish on push to `main`

Runs automatically on every push to `main` and can also be triggered manually.

### What the publish workflow does

| Step | Details |
| ------ | --------- |
| **Restore** | `dotnet restore` against the `.slnx` solution file |
| **Build** | `dotnet build -c Release` (no restore) |
| **Test** | `dotnet test -c Release --no-build` |
| **Check version bumps** | Runs `dotnet-version check` against the previous release tag — blocks publish if any package version was not bumped |
| **Collect metadata** | Runs the freshly built `dotnet-version` with the solution file and `-f GeneratePackageOnBuild=true` to enumerate all package names + versions |
| **Tag commits** | Pushes an annotated git tag per package (`<Name>-v<Version>`) plus one combined release tag |
| **Publish to NuGet** | Pushes every `.nupkg` to `nuget.org` with `--skip-duplicate` |
| **GitHub Release** | Creates a GitHub release on the combined tag and uploads all `.nupkg` files as assets |

### Required repository secret

| Secret | Description |
| -------- | ------------- |
| `NUGET_API_KEY` | API key from [nuget.org](https://www.nuget.org/account/apikeys) with push permission for the package(s) |

Add it under **Settings → Secrets and variables → Actions → New repository secret**.

### Manual dispatch

The workflow can be triggered manually from the **Actions** tab.  
An optional `slnx_file` input lets you override the solution path
(default: `DotnetVersionReader.slnx`).
