# ZomniverseGitPet

ZomniverseGitPet is a lightweight desktop Git guardian for Windows. It keeps a small, always-on-top pet near your workspace and turns repository hygiene into a visible, low-friction habit.

Double-click the pet to open the Guardian dashboard. ZomniverseGitPet can watch a repository, review changed files and diffs, run project-specific test hooks, create one-click local restore points, explicitly push committed history to an existing `origin` remote, display recent commits, and run Git health checks. Automatic verified checkpoints are an opt-in workflow and are disabled by default.

ZomniverseGitPet is especially useful during AI-assisted development, where several rapid edits can make it harder to see what changed or when a safe local checkpoint should be created. It never pushes automatically, creates remotes, force-resets, or cleans a repository.

## Project status

ZomniverseGitPet is an early public preview. The native C# application replaces the original PowerShell proof of concept, which remains in `prototype/powershell/` as a reference and rollback implementation.

Current platform support: Windows 10/11, x64, with Git for Windows available as `git.exe`.

## Features

- Frameless, draggable, always-on-top pet with system tray integration
- Single-instance behavior; a second launch activates the existing Guardian
- Background repository monitoring without blocking the UI
- Branch and changed-file overview with `.gitignore` support
- Diff review, recent history, configurable test hooks, and `git fsck`
- Confirmed full-working-tree restore points using ordinary local Git commits
- Explicit, confirmed manual push to an existing `origin` remote on the current branch
- Suspicious-file safeguards and append-only local audit logging
- Typed per-user configuration under `%LOCALAPPDATA%\ZomniverseGitPet`

## Build

Requirements: Windows, the .NET 8 SDK, and Git for Windows.

```powershell
dotnet build ZomniverseGitPet.sln -c Release
dotnet run --project tests/ZomniverseGitPet.Tests/ZomniverseGitPet.Tests.csproj -c Release
```

Create a self-contained single executable:

```powershell
dotnet publish src/ZomniverseGitPet/ZomniverseGitPet.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The single executable is written beneath `src/ZomniverseGitPet/bin/Release/net8.0-windows/win-x64/publish/`.

## Local development build

Run `scripts/publish-local.ps1` to create the runnable self-contained executable in the sibling `ZomniverseGitPet_Releases/current` directory. The output location is derived from the repository location, so no machine-specific path is stored in the project.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-local.ps1
```

Normal compiler output remains under the ignored `bin/` and `obj/` directories; the runnable local copy is outside the source repository.

## Configuration

On first launch, right-click the pet and choose **Choose repository**. ZomniverseGitPet writes its typed JSON configuration and append-only audit trail beneath `%LOCALAPPDATA%\ZomniverseGitPet`. No repository path or baseline commit is built into the application.

Test hooks are ordinary command strings run from the selected repository. Edit `config.json` while ZomniverseGitPet is closed, then restart it. Treat test commands as trusted local configuration.

## Restore-point and push safety

A restore point previews all current non-ignored changes, asks for confirmation, stages with `git add -A`, and creates a normal local commit. The separate **Push** action is manual-only: it verifies that an existing `origin` remote and named current branch are available, shows the destination and latest commit, and asks for explicit confirmation before running the equivalent of `git push origin <current-branch>`.

The Push action sends committed history only. It does not stage or commit working-tree changes, create/configure remotes, force-push, reset, or clean. Automatic checkpoints are off by default and never trigger a push.

## Roadmap

- In-app settings editor for tests and checkpoint policy
- File-system-assisted refresh with polling fallback
- Signed release artifacts and installer packaging
- Broader accessibility and multi-monitor refinements
- Task/worktree-aware checkpoints for parallel AI agents
- Additional automated integration and UI tests

## License

ZomniverseGitPet is available under the [MIT License](LICENSE).

