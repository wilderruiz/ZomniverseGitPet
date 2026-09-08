# ZomniverseGitPet

ZomniverseGitPet is a lightweight desktop Git guardian for Windows. It keeps a small, always-on-top purple fox near your workspace and turns repository hygiene into a visible, low-friction habit.

Double-click the pet to open the Guardian dashboard. ZomniverseGitPet can watch and switch between projects, inspect folders before Git setup, safely initialize local Git metadata, suggest reviewable `.gitignore` rules, review changed files and diffs, run project-specific test hooks, create one-click local restore points, explicitly push committed history to an existing `origin` remote, display recent commits, and run Git health checks.

ZomniverseGitPet is especially useful during AI-assisted development, where several rapid edits can make it harder to see what changed or when a safe local checkpoint should be created. It never pushes automatically, creates remotes automatically, force-resets, or cleans a repository.

## Project status

ZomniverseGitPet is an early public preview. The native C# application replaces the original PowerShell proof of concept, which remains in `prototype/powershell/` as a reference and rollback implementation.

Current platform support: Windows 10/11, x64, with Git for Windows available as `git.exe`.

## Features

- Friendly purple fox desktop pet with repository-state visuals and system tray integration
- Single-instance behavior; a second launch activates the existing Guardian
- Background repository monitoring without blocking the UI
- Project menu with up to 20 recently used repositories
- Folder suitability inspection before a project is accepted
- Safe local initialization of ordinary folders with `git init -b main`
- Nested-repository detection that offers the existing parent repository instead of silently creating another `.git`
- Reviewable `.gitignore` recommendations for common build output, IDE files, caches, logs, environment files, and key material
- Existing `.gitignore` content is preserved; only explicitly accepted missing rules are appended
- Branch and changed-file overview with `.gitignore` support
- Diff review, recent history, configurable test hooks, and `git fsck`
- Confirmed full-working-tree restore points using ordinary local Git commits
- Explicit, confirmed manual push to an existing `origin` remote on the current branch
- Suspicious-file safeguards and append-only local audit logging
- Typed per-user configuration under `%LOCALAPPDATA%\ZomniverseGitPet`

## Project workflow

Use **Choose Repo / Projects** from Guardian or the pet menu. GitPet keeps up to 20 recent projects and lets you switch between them quickly.

When you select a folder, GitPet classifies it before doing anything:

- **Ready** — an existing readable Git repository; GitPet opens it immediately.
- **Nested repository** — the selected folder sits inside another Git repository; GitPet identifies the real root and offers to use it.
- **Can prepare** — an ordinary readable folder with no Git metadata; GitPet can initialize local Git only after confirmation.
- **Invalid/unavailable** — existing Git metadata is unreadable, Git is unavailable, or the folder cannot be accessed; GitPet explains the problem and does not overwrite anything.

### Preparing an ordinary folder

For a non-Git folder, GitPet presents a preparation dialog with optional `.gitignore` recommendations. High-confidence and security-sensitive candidates are preselected; project-dependent suggestions remain unchecked for review.

After explicit confirmation, GitPet performs only:

```text
git init -b main
```

It then verifies the repository root and appends only the `.gitignore` rules you accepted. Preparation does **not** create a Git hosting repository, configure `origin`, stage files, create a commit, or push anything.

### `.gitignore` hygiene

For new or existing repositories, use **Review .gitignore suggestions…** from the Projects menu. GitPet samples the project tree and can suggest rules such as:

- `bin/`, `obj/`, `.vs/`, `.idea/`
- `node_modules/`, Python caches and virtual environments
- `.DS_Store`, `Thumbs.db`, `*.user`, `*.suo`, `*.log`
- `.env`, `.env.*`, `*.pem`, `*.key`
- review-only candidates such as `dist/`, `coverage/`, `.cache/`, `tmp/`, and `temp/`

Nothing is added automatically. Existing `.gitignore` content is left in place and accepted missing rules are appended in a small ZomniverseGitPet section.

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

ZomniverseGitPet writes its typed JSON configuration and append-only audit trail beneath `%LOCALAPPDATA%\ZomniverseGitPet`. The configuration stores the active repository and an MRU registry of up to 20 recent repositories; no machine path is built into the application or public source tree.

Test hooks are ordinary command strings run from the selected repository. Edit `config.json` while ZomniverseGitPet is closed, then restart it. Treat test commands as trusted local configuration.

## Restore-point and push safety

A restore point previews all current non-ignored changes, asks for confirmation, stages with `git add -A`, and creates a normal local commit. The separate **Push** action is manual-only: it verifies that an existing `origin` remote and named current branch are available, shows the destination and latest commit, and asks for explicit confirmation before running the equivalent of `git push origin <current-branch>`.

The Push action sends committed history only. It does not stage or commit working-tree changes, create/configure remotes, force-push, reset, or clean. Automatic checkpoints are off by default and never trigger a push.

## Roadmap

- Futuristic Guardian visual-system pass and custom application chrome
- Canonical fox application/taskbar/tray icon
- Micro-animation and repository-state transitions
- Richer shared repository snapshot/state model across pet and Guardian
- Public screenshot/feature gallery and polished release presentation
- In-app settings editor for tests and checkpoint policy
- File-system-assisted refresh with polling fallback
- Signed release artifacts and installer packaging
- Broader accessibility and multi-monitor refinements
- Task/worktree-aware checkpoints for parallel AI agents
- Additional automated integration and UI tests

## License

ZomniverseGitPet is available under the [MIT License](LICENSE).
