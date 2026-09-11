# Project model

Source: `Models.cs`, `AppConfig.cs`, `ProjectInspector.cs`, `LogicalProjectScopeRuntime.cs`, `ProjectScopeSelectionForm.cs` / `ProjectScopeSelectionModel.cs`, `ProjectScopePlanner.cs`.

## `RecentRepositoryEntry` — the unit of a "project"

```csharp
public string Id { get; set; }
public string Path { get; set; }              // this project's scope root (may be a subfolder)
public string RepositoryRoot { get; set; }     // the shared .git repository root
public string DisplayName { get; set; }
public bool TrackEverything { get; set; }
public List<ProjectScopeConfigEntry> ScopeEntries { get; set; }
public List<string> TestCommands { get; set; }
public DateTimeOffset LastOpenedUtc { get; set; }
```

Several `RecentRepositoryEntry` rows can share one `RepositoryRoot` — that's the whole basis for [logical projects](../user/LOGICAL_PROJECTS.md). `Path` is the project's own root for display/navigation purposes; it can equal `RepositoryRoot` (a whole-repository project) or be an arbitrary subfolder.

## Inspecting a folder

`ProjectInspector.InspectAsync` classifies a candidate folder before GitPet does anything with it:

| Classification | Meaning |
| --- | --- |
| `Ready` | Already a Git repository root — open directly. |
| `NestedRepository` | Inside an existing repository, not its root — register as a new logical project against the parent's `RepositoryRoot`. |
| `CanPrepare` | An ordinary folder, not yet a repository — offer preparation. |
| `InvalidRepository` / `GitUnavailable` / `Unavailable` | Something's wrong; explain rather than proceed. |

## Resolving the active project's scope

`LogicalProjectScopeRuntime` is a static runtime bound to whichever project is currently active. Everything else in the app asks it, rather than reading `AppConfig` directly:

- `GetWorkingDirectory(repositoryRoot)` — the active project's `Path`, or the repo root if it doesn't match.
- `GetScopeEntries` / `GetPathspecs` — the active project's in-scope relative paths (empty for `TrackEverything`).
- `ContainsPath(path)` — is this repo-relative path in scope? Directories match as a prefix; `.gitignore` is always implicitly in scope.
- `MigrateLegacyScope` — one-time migration of a pre-multi-project config's root-level scope onto the (still whole-repo) project record.

## Building a scope from a tree selection

`ProjectScopeSelectionForm`/`ProjectScopeSelectionModel` implement a tri-state (checked / unchecked / indeterminate) tree, lazily populated, with per-subtree overrides layered on top of any restored selection. `ProjectScopePlanner.Create` turns the raw selection into a `ProjectScopePlan`: it normalizes and dedupes entries, detects nested Git repositories under the root (and always excludes them from scope), and can render the plan as `.gitignore`-style rules via `BuildIgnoreRules`.

The same selection can be expressed as text instead of clicks — see [Advanced Project Allow Lists](../user/ALLOW_LISTS.md) and `ProjectScopeAllowList.Resolve`, which parses and validates pasted paths against the same rules (must be inside the root, not `.git`, not a nested repository, must exist).

## `AppConfig` — what's persisted

Saved as JSON at `%LOCALAPPDATA%\ZomniverseGitPet\config.json` (atomic write: temp file + rename). See [Configuration](../reference/CONFIGURATION.md) for the full field list and [AppData Layout](../reference/APPDATA_LAYOUT.md) for every adjacent file (allow lists, standalone-publishing links, audit log).
