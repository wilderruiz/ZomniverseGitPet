# Filesystem and sandbox considerations

## Dubious ownership / cloud-synced and external drives

Git itself refuses to operate on a repository whose ownership metadata looks wrong for the current user — a common situation on external drives, network shares, and cloud-synced folders (Dropbox, OneDrive), regardless of GitPet. When `git rev-parse --show-toplevel` fails with this specific error, GitPet:

1. Shows `SafeDirectorySetupForm` explaining the situation in plain language, with the raw Git error visible.
2. Only on explicit approval, checks the existing global `safe.directory` list (to avoid a duplicate entry) and adds **the exact folder path** — never a wildcard, never a parent folder, never every folder on the system.

This is the only place GitPet modifies global Git configuration, and it only ever adds, never removes, entries.

## The isolated publishing workspace

[Standalone logical-project publishing](../developer/PUBLISHING_ARCHITECTURE.md) creates its own Git working copy under `%LOCALAPPDATA%\ZomniverseGitPet\Publishing\<projectId>\` — outside any repository the user opened, and verified never to be a subdirectory of the source repository. This keeps a scoped project's isolated push history from ever being nested inside (or confused with) the parent repository's own working tree.

## Keeping the DEV executable outside cloud-synced folders

`scripts/publish-local.ps1` writes the development build to `%LOCALAPPDATA%\ZomniverseGitPet\DEV\DEV-ZomniverseGitPet.exe` rather than anywhere inside a cloud-synced source folder, specifically so a running executable file is never mid-sync while in use (this repository itself, notably, lives inside a Dropbox-synced folder).

## Single-instance enforcement

A single named `Mutex` (SHA-256-hashed, keyed to the current Windows user) prevents two copies of GitPet from running at once; a second launch instead activates the existing instance over a named pipe. This avoids two processes writing to the same `AppConfig`/audit-log files concurrently in the ordinary case — see [`AuditLog`'s own concurrency handling](../reference/APPDATA_LAYOUT.md#auditjsonl) for what happens even so.
