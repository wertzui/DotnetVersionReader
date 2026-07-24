# DotnetVersionReader

![DotnetVersionReader logo](logo.png)

A .NET global tool for reading version information from `.csproj`, `.sln`, and `.slnx` files,
for enforcing version bumps in pull requests, and for showing which projects had their version changed.

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

## Usage

Note that `dotnet version` and `dotnet-version` can both be used to invoke the application.
The first one is through the `dotnet` CLI, while the second one directly invokes this programm.

```bash
dotnet version [command] [options]

Commands:
  read    Reads and displays version information from .csproj files. (default)
  check   Checks that every project whose source files have changed has had its version bumped.
  diff    Shows projects whose version has changed (or that are new) relative to a base branch.
```

---

### `dotnet version read` — read versions (default)

Reads and displays version information. This is the **default command**: running
`dotnet version` with no subcommand is equivalent to `dotnet version read`.

```bash
# Both forms are equivalent:
dotnet version          [--input <path>] [options]
dotnet version read     [--input <path>] [options]
```

#### Options

| Option | Short | Description |
| -------- | ------- | ------------- |
| `--input` | `-i` | Path to a `.csproj`, `.sln`, `.slnx` file **or** a folder. Defaults to the current directory. |
| `--output` | `-o` | Output format: `json` (default), `table`, `list`, or `version` (single project only). |
| `--filter` | `-f` | Filter in the form `XmlNode=Value`. Value can be a regex. Repeatable. |
| `--schema` | | Print the JSON schema for `--output json` and exit. Defaults to `false`. |

#### Version resolution

The tool follows MSBuild semantics:

1. If `<Version>` is set, it is used as-is.
2. Otherwise the version is `<VersionPrefix>` (default `1.0.0`) optionally followed by `-<VersionSuffix>`.

#### Examples

```bash
# Current directory – JSON output (default, both forms are equivalent)
dotnet version
dotnet version read

# Specific solution file – table output
dotnet version read --input MySolution.slnx --output table
dotnet version read -i MySolution.slnx -o table

# Only projects that generate a NuGet package
dotnet version read --filter "GeneratePackageOnBuild=true"

# Combine multiple filters (all must match)
dotnet version read -i MySolution.slnx -f "TargetFramework=^net10\.0$" -f "GeneratePackageOnBuild=true"
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
| --------- | ---------- | ----- | ----- | ----- | ------ |
| MyLibrary | 2.1.0-rc.1 | 2     | 1     | 0     | rc.1   |
| MyApp     | 1.0.0      | 1     | 0     | 0     |        |
```

#### Sample list output

```text
MyLibrary 2.1.0-rc.1
MyApp 1.0.0
```

#### Sample version output

```text
2.1.0-rc.1
```

---

### `dotnet version check` — enforce version bumps in PRs

Checks that every project whose source files have changed (compared to a base branch)
has had its version bumped. Designed to run as a CI gate on pull requests.

```bash
dotnet version check [--base <ref>] [--input <path>] [--head <ref>] [--output <format>] [--filter <XmlNode=Value>]...

# Short aliases (--base defaults to origin/main):
dotnet version check [-b <ref>] [-i <path>] [--head <ref>] [-o <format>] [-f <XmlNode=Value>]...
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
dotnet version check
dotnet version check --base origin/main

# Scope to a specific solution file
dotnet version check --input MySolution.slnx --base origin/main
dotnet version check -i MySolution.slnx -b origin/main

# Table output
dotnet version check --input MySolution.slnx --base origin/main --output table

# Single project, bare version output (useful for scripts)
dotnet version check --input src/MyLib/MyLib.csproj --base origin/main --output version

# Only check projects that produce a NuGet package
dotnet version check --input MySolution.slnx --base origin/main --filter "GeneratePackageOnBuild=true"
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
          dotnet version: 10.0.x

      - name: Install dotnet version
        run: dotnet tool install --global DotnetVersionReader

      - name: Check version bumps
        run: dotnet version check --input MySolution.slnx --base origin/main
```

> **Important:** `fetch-depth: 0` (or at least enough history to reach the base branch) is required; a shallow clone will cause `git diff` to fail.

---

### `dotnet version diff` — show version changes relative to a base branch

Shows all projects whose version has changed (or that are brand-new) compared to a base branch,
**or** whose `<PackageReference>`/`<ProjectReference>` entries changed while the project's own
version stayed the same — in which case a new `<VersionPrefix>`/`<VersionSuffix>` is suggested
based on semantic versioning.
Unlike `check`, this command **never exits with a non-zero code based on results** — it is a
pure informational diff, useful for release notes, changelogs, or scripting.

```bash
dotnet version diff [--base <ref>] [--input <path>] [--head <ref>] [--output <format>] [--filter <XmlNode=Value>]... [--bump]

# Short aliases (--base defaults to origin/main):
dotnet version diff [-b <ref>] [-i <path>] [--head <ref>] [-o <format>] [-f <XmlNode=Value>]... [--fix]
```

#### Options

| Option | Short | Description |
| -------- | ------- | ------------- |
| `--input` | `-i` | Path to a `.csproj`, `.sln`, `.slnx` file **or** a folder. Defaults to the current directory. |
| `--base` | `-b` | The git ref to compare against. Defaults to `origin/main`. |
| `--head` | | The git ref for the current state. Defaults to `HEAD`. |
| `--output` | `-o` | Output format: `json` (default), `table`, `list`, or `version` (single project only). |
| `--filter` | `-f` | Filter in the form `XmlNode=Value`. Only matching projects are considered. Value can be a regex. Repeatable. |
| `--bump` | `--fix` | Automatically write the suggested version into each affected `.csproj` file (only for projects with status `DependenciesChanged`). Defaults to `false`. |

#### Exit codes

| Code | Meaning |
| ------ | --------- |
| `0` | Command completed successfully (regardless of how many projects changed). |
| `2` | Usage or argument error (bad input path, git not found, etc.). |

#### How it works

Uses the same git/dependency-graph pipeline as `check` (steps 1–4 are identical), with one
addition: a project is also considered "affected" (step 4) when the nearest
`Directory.Packages.props` file that applies to it (NuGet Central Package Management, found by
walking up from the project's directory — it usually lives several levels above, shared by many
projects) is among the changed files, even though that file is not physically inside the
project's own directory. This is what makes a **CPM-only** package bump (i.e. only
`Directory.Packages.props` changed, no `.csproj` touched) show up in `diff` for every project
that references an affected package — and bubbles up transitively to their consumers too.

At step 5, for each affected project:

1. If the project is brand-new (didn't exist on `<base>`), it is included with status `NewProject`.
2. Otherwise, its resolved version on `<head>` is compared to `<base>`. If they differ, it is
   included with status `Bumped`.
3. Otherwise (the version is unchanged), its `<PackageReference>` and `<ProjectReference>`
   entries are compared between `<head>` and `<base>`:
   - A `<PackageReference>` version bump is classified as a **major**, **minor**, or **patch**
     semantic-versioning change (based on which component of the dependency's version changed).
     Package versions that are not declared inline are resolved from the nearest
     `Directory.Packages.props` file (NuGet Central Package Management) — read from the same git
     ref being compared, so history is respected even if the props file has since changed.
   - An added `<PackageReference>` or `<ProjectReference>` counts as a **minor** change; a
     removed one counts as a **major** change.
   - If any dependency changed, the project is included with status `DependenciesChanged` and
     `SuggestedVersionPrefix`/`SuggestedVersionSuffix` are computed by bumping the project's
     current version according to the *most severe* change found (major > minor > patch). The
     suggested suffix is always empty, since a suggested bump drops any pre-release suffix.
4. Projects with no version change and no dependency changes are silently omitted.

When `--bump` (alias `--fix`) is passed, every project reported with status `DependenciesChanged`
has its suggested version written directly into its `.csproj` file before the result is printed:

- If the project already has a `<Version>` element, it is updated in place with the combined
  `SuggestedVersionPrefix`/`SuggestedVersionSuffix` string.
- Otherwise `<VersionPrefix>` is created or updated, and `<VersionSuffix>` is created/updated (or
  removed, since a suggested bump always has an empty suffix) alongside it, in the project's
  first `<PropertyGroup>` (a new one is created if none exists).
- Projects with status `Bumped` or `NewProject` are left untouched — the author already handled
  those (or there is nothing to bump for a brand-new project).

> **Tip:** Run `diff` without `--bump` first to review the suggested versions, then re-run with
> `--bump` (or `--fix`) once you're happy with them — e.g. as a pre-commit step or a dedicated
> "auto-bump" CI job that commits the result back to the PR branch.

#### Examples

```bash
# Show changed versions against origin/main (default)
dotnet version diff
dotnet version diff --base origin/main

# Scope to a specific solution file
dotnet version diff --input MySolution.slnx --base origin/main
dotnet version diff -i MySolution.slnx -b origin/main

# Table output
dotnet version diff --input MySolution.slnx --base origin/main --output table

# Simple list output – handy for release notes
dotnet version diff --input MySolution.slnx --base origin/main --output list

# Only projects that produce a NuGet package
dotnet version diff --input MySolution.slnx --base origin/main --filter "GeneratePackageOnBuild=true"

# Automatically apply the suggested version bump to affected .csproj files
dotnet version diff --input MySolution.slnx --base origin/main --bump
dotnet version diff --input MySolution.slnx --base origin/main --fix
```

#### Sample JSON output

```json
[
  {
    "Name": "MyLib",
    "FilePath": "src/MyLib/MyLib.csproj",
    "HeadVersion": "2.0.0",
    "BaseVersion": "1.0.0",
    "Status": "Bumped",
    "DependencyChanges": [],
    "SuggestedVersionPrefix": null,
    "SuggestedVersionSuffix": null,
    "SuggestedVersion": null
  },
  {
    "Name": "MyNewLib",
    "FilePath": "src/MyNewLib/MyNewLib.csproj",
    "HeadVersion": "1.0.0",
    "BaseVersion": null,
    "Status": "NewProject",
    "DependencyChanges": [],
    "SuggestedVersionPrefix": null,
    "SuggestedVersionSuffix": null,
    "SuggestedVersion": null
  },
  {
    "Name": "MyApp",
    "FilePath": "src/MyApp/MyApp.csproj",
    "HeadVersion": "1.2.3",
    "BaseVersion": "1.2.3",
    "Status": "DependenciesChanged",
    "DependencyChanges": [
      {
        "Kind": "Package",
        "Name": "Newtonsoft.Json",
        "BaseVersion": "13.0.1",
        "HeadVersion": "13.0.2",
        "BumpType": "Patch"
      }
    ],
    "SuggestedVersionPrefix": "1.2.4",
    "SuggestedVersionSuffix": "",
    "SuggestedVersion": "1.2.4"
  }
]
```

Possible `Status` values:

| Value | Meaning |
| ------- | --------- |
| `Bumped` | The project's own version was bumped relative to the base branch. |
| `NewProject` | The project did not exist on the base branch. |
| `DependenciesChanged` | The project's own version is unchanged, but a `<PackageReference>` or `<ProjectReference>` changed. See `SuggestedVersionPrefix`/`SuggestedVersionSuffix`/`SuggestedVersion` for the recommended new version. |

Each entry in `DependencyChanges` has a `Kind` (`Package` or `Project`) and a `BumpType`
(`Major`, `Minor`, or `Patch`) describing the semantic-versioning severity of that single change:

| BumpType | When |
| ---------- | ------ |
| `Major` | A dependency was removed, or its major version component changed. |
| `Minor` | A dependency was added, or its minor version component changed. |
| `Patch` | A dependency's patch version (or pre-release suffix) changed. |

#### Sample table output

```
| Name  | HeadVersion | BaseVersion | Status              | SuggestedVersion |
|-------|-------------|-------------|----------------------|------------------|
| MyLib | 2.0.0       | 1.0.0       | Bumped               |                  |
| MyApp | 1.2.3       | 1.2.3       | DependenciesChanged  | 1.2.4            |
```

#### Sample list output

```text
MyLib 2.0.0
MyApp 1.2.4
```

> **Note:** In `list` and `version` output, a project with status `DependenciesChanged` shows the
> *suggested* version rather than its (unchanged) current version.

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
