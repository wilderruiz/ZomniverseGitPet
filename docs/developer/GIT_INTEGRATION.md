# Git integration

Source: `GitService.cs`, `GitServiceCloneExtensions.cs`, `GitRepositoryAddress.cs`, `GitIgnoreAdvisor.cs`, `GitIgnoreCleaner.cs`, `GitIgnoreRuleLibrary.cs`, `ScopedGitIgnoreAdvisor.cs`, `SafeDirectorySetupForm.cs`, `IgnoredFileSavePolicy.cs`.

## `GitService` — the only place that runs `git.exe`

`GitService` wraps `Process` invocations of `git.exe` and exposes status, log, diff, commit, push, pull, init, and `fsck`. Every other class that needs Git goes through it rather than shelling out itself. It also owns the one recovery flow Git itself can force on GitPet: **dubious ownership**.

### Dubious ownership / `safe.directory`

`GetRepositoryRootAsync` runs `git rev-parse --show-toplevel`; if that fails with the "detected dubious ownership" error (typical for external, network, or cloud-synced drives), GitPet shows `SafeDirectorySetupForm` for explicit consent, then runs:

```bash
git config --global --add safe.directory <exact-path>
```

Only the exact folder path is added — never a wildcard — and GitPet first checks the existing `safe.directory` list to avoid duplicate entries. See [Filesystem and Sandbox](../safety/FILESYSTEM_AND_SANDBOX.md).

## Cloning

`GitServiceCloneExtensions.CloneRepositoryAsync` verifies the destination folder is empty, runs `git clone`, and verifies the result with `git rev-parse --show-toplevel`. `GitRepositoryAddress`/`GitRepositoryAddressParser` normalizes an owner/repo shorthand, an HTTPS URL, or an SSH URL into one parsed address before any of this runs.

## `.gitignore` handling — four distinct roles

| Class | Role |
| --- | --- |
| `GitIgnoreAdvisor` | Scans one directory tree (breadth-first, capped, pruning heavy folders) for common generated/sensitive patterns and proposes rules; also reads/previews/appends. |
| `GitIgnoreCleaner` | Pure text utility: finds and removes duplicate rule lines, preserving line endings. |
| `GitIgnoreRuleLibrary` | A static, curated catalog of preset rule bundles (privacy / generated / system / archive) plus a validated custom-rule builder, for the picker UI. |
| `ScopedGitIgnoreAdvisor` | Project-scope-aware orchestrator: scans only the selected scope (or everything, if `TrackEverything`), and discovers (but never modifies) nested `.gitignore` files already in effect. Contains `ProjectGitIgnoreComposer`, the actual writer, which maintains a clearly delimited "managed project scope" block in `.gitignore` and can read its own previously-written block back out. |

## Ignored-file explicit tracking

`IgnoredFileSavePolicy` parses `git check-ignore -v` output into `IgnoredProjectFile` records (which rule, which file, which line) and splits a Save's changed files into normal files, explicitly-approved-ignored files, and skipped-ignored files. Approved ignored files are force-added with their **exact path**, never a directory or glob:

```bash
git add -f -- <exact-path>
git add -A -- <normal-files>
```

This preflight never stages anything by itself — it only computes what *would* happen so `IgnoredProjectFilesDialog` can ask the user first. See [Send Preflight](../safety/SEND_PREFLIGHT.md).
