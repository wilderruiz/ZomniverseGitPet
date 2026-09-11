# Configuration

`AppConfig` (`src/ZomniverseGitPet/AppConfig.cs`), persisted as JSON at `%LOCALAPPDATA%\ZomniverseGitPet\config.json` via `ConfigStore.Load`/`Save` (atomic: write to a temp file, then rename).

| Field | Purpose |
| --- | --- |
| `SchemaVersion` | Current normalized value: 5. Used to run one-time migrations on load. |
| `RepositoryPath` | Active repository root, kept for backward compatibility. |
| `ActiveProjectId` | Which logical project (see [Project Model](../developer/PROJECT_MODEL.md)) is currently active. |
| `RecentRepositories` | Up to 20 `RecentRepositoryEntry` records (most recent first) — see below. |
| `PollSeconds` | Remote-poll interval; default 20, floored to 10 by the watcher itself. |
| `AutomaticCheckpointsEnabled` | Off by default; opt-in silent local-only Saves. |
| `QuietMinutes` | Minimum idle period before an automatic checkpoint fires; default 10. |
| `RequireTestsForAutomaticCheckpoint` | Default true — an automatic checkpoint won't fire if the project's saved tests fail. |
| `OnboardingCompleted` | Whether first-run setup has been finished. |
| `ConnectionMode` | `unconfigured` / `github` / `local-git-only` — a UI label only, see [GitHub Integration](../developer/GITHUB_INTEGRATION.md). |
| `TestCommands` | Legacy (v1/v2) root-level test-command list; migrated onto the active project's own `TestCommands` on load, then cleared. |
| `SuspiciousPathPatterns` | Default regex list flagged before Save: `.env`, `.pem`, `.key`, `id_rsa`, `credentials`, `secrets?.`, `password`, `token`. |

## `RecentRepositoryEntry` (one per logical project)

| Field | Purpose |
| --- | --- |
| `Id` | Stable identifier, used to key per-project state in other files (allow lists, standalone-publishing links). |
| `Path` | This project's own scope root (may be a subfolder of the repository). |
| `RepositoryRoot` | The shared `.git` repository root — several entries can share one. |
| `DisplayName` | User-chosen project name. |
| `TrackEverything` | Whole-repository project vs. scoped. |
| `ScopeEntries` | Selected relative paths, when scoped. |
| `TestCommands` | Up to 20 saved shell commands for this project. |
| `LastOpenedUtc` | Drives the "most recent first" ordering and the 20-entry cap. |

See [AppData Layout](APPDATA_LAYOUT.md) for the other files GitPet keeps alongside `config.json`, and [File Formats](FILE_FORMATS.md) for their exact shapes.
