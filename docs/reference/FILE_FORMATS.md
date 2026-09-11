# File formats

JSON shapes for the files GitPet reads and writes itself. Field names below reflect the C# model properties; actual JSON casing follows the project's default `System.Text.Json` serialization.

## `release-manifest.json` (published with each GitHub Release)

```json
{
  "SchemaVersion": 1,
  "Version": "<x.y.z>",
  "Channel": "stable",
  "ReleaseTag": "v<x.y.z>",
  "ReleasePage": "https://github.com/<owner>/<repo>/releases/tag/v<x.y.z>",
  "Installer": {
    "FileName": "ZomniverseGitPet-Setup-<x.y.z>.exe",
    "Sha256": "<64 hex characters>",
    "DownloadUrl": "https://github.com/<owner>/<repo>/releases/download/v<x.y.z>/ZomniverseGitPet-Setup-<x.y.z>.exe"
  }
}
```

Consumed by `ApplicationUpdateService` (see [Update System](../developer/UPDATE_SYSTEM.md)). `Channel` must equal `"stable"`; `Sha256` must be exactly 64 hex characters and is re-verified against the actual downloaded bytes before install.

## `SHA256SUMS.txt`

Plain checksum-tool format, one line per artifact:

```
<sha256 hex>  ZomniverseGitPet-Setup-<x.y.z>.exe
<sha256 hex>  ZomniverseGitPet-<x.y.z>-win-x64-portable.exe
```

## `project-allow-lists.json` (`%LOCALAPPDATA%\ZomniverseGitPet\`)

A list of per-project records:

```json
{
  "ProjectId": "<guid>",
  "RepositoryRoot": "<path>",
  "ProjectPath": "<path>",
  "ProjectName": "<display name>",
  "Text": "packages/widgets/\n# a comment\nREADME.md\n",
  "UpdatedUtc": "2026-09-11T23:04:00Z"
}
```

Matched first by `ProjectId`; falls back to the `(RepositoryRoot, ProjectPath, ProjectName)` triple for records saved before a project had an ID assigned. Saving empty text deletes the record. See [Advanced Project Allow Lists](../user/ALLOW_LISTS.md).

## `standalone-publishing.json` (`%LOCALAPPDATA%\ZomniverseGitPet\`)

```json
{
  "ProjectId": "<guid>",
  "RemoteUrl": "https://github.com/<owner>/<repo>.git",
  "LastPublishedSourceCommit": "<sha>",
  "LastPublishedFingerprint": "<sha256 of pathspecs + ls-tree output>",
  "LastPublishedUtc": "2026-09-11T23:04:00Z"
}
```

See [Publishing Architecture](../developer/PUBLISHING_ARCHITECTURE.md) for how the fingerprint is computed and used.

## `audit.jsonl` (`%LOCALAPPDATA%\ZomniverseGitPet\`)

One object per line, no trailing comma, no wrapping array:

```json
{"timestamp": "2026-09-11T23:04:12Z", "event": "standalone_project_published", "data": {"projectId": "<guid>"}}
```

See [AppData Layout](APPDATA_LAYOUT.md#auditjsonl).

## `config.json`

See [Configuration](CONFIGURATION.md) for the full field list — it's a single JSON object, not a list.
