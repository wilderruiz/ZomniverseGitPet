# AppData layout

Everything GitPet persists lives under `%LOCALAPPDATA%\ZomniverseGitPet\` (a real path looks like `C:\Users\<you>\AppData\Local\ZomniverseGitPet\` — this documentation never spells out a specific machine's actual path).

| Path | Contents |
| --- | --- |
| `config.json` | GitPet's own settings and recent-project registry — see [Configuration](CONFIGURATION.md). |
| `project-allow-lists.json` | Per-project [Advanced Project Allow List](../user/ALLOW_LISTS.md) text, keyed by project ID (falling back to repository root + project path + name for older records). |
| `standalone-publishing.json` | Per-project standalone-publishing link state: linked remote URL, last-published source commit, last-published content fingerprint. |
| `window-layout.json` | Persisted window position/size, restored on next launch. |
| `audit.jsonl` | Append-only activity log — see below. |
| `Publishing\<projectId>\` | Isolated Git working copy for a scoped logical project's standalone publish — see [Publishing Architecture](../developer/PUBLISHING_ARCHITECTURE.md). |
| `DEV\DEV-ZomniverseGitPet.exe` | The development build produced by `scripts/publish-local.ps1`, plus its `DEV-ZGitPet.lnk` shortcut. Not present unless a developer has run that script. |

## `audit.jsonl`

One JSON object per line: `{"timestamp": <UTC ISO 8601>, "event": "<name>", "data": {...}}`, appended by `AuditLog.WriteAsync`. Example event names include `checkpoint_blocked_suspicious_paths`, `application_update_available`, `repository_cloned`, `logical_project_configured`, `health_check`, `standalone_project_published`, and `connection_mode_selected`.

Writes are serialized process-wide by a `SemaphoreSlim`, with up to 4 retries (25ms × attempt backoff) on a transient `IOException`; any failure is swallowed to a debug trace rather than surfaced to the user — audit logging is deliberately best-effort and must never block or crash GitPet itself. This guards concurrent writes *within one process*; GitPet's own single-instance mutex (see [Filesystem and Sandbox](../safety/FILESYSTEM_AND_SANDBOX.md)) is what keeps a second process from writing the same file at the same time in ordinary use.

See [File Formats](FILE_FORMATS.md) for the JSON shapes of `config.json`, `project-allow-lists.json`, and `standalone-publishing.json`.
