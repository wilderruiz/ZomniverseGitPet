<p align="center">
  <img src="mockups/pet/svg/pet_happy_01.svg" width="180" alt="ZomniverseGitPet purple fox guardian">
</p>

<h1 align="center">ZomniverseGitPet</h1>

<p align="center"><strong>A desktop Git guardian for human and AI-assisted development.</strong></p>

ZomniverseGitPet is a lightweight Windows desktop companion that keeps a small purple fox near your workspace and turns Git safety into a visible, low-friction habit. It is designed for experienced developers, people working with AI coding agents, and users who do not want to memorize Git commands just to keep their projects safe.

Double-click the pet to open the Guardian Console. GitPet can watch and switch between projects, inspect ordinary folders before Git setup, safely initialize local Git metadata, explain and preview `.gitignore` changes, review changed files and diffs, run project-specific test hooks, create local restore points, explicitly push committed history to an existing `origin` remote, display recent commits, and run Git health checks.

GitPet never pushes automatically, never creates remotes automatically, and never uses destructive operations such as `reset --hard` or `clean`.

## Guardian Console — 0.3

Version 0.3 introduces the visual system used by the real desktop application:

- dark graphite / near-black working surfaces
- violet structure and status surfaces
- hot-pink emphasis for deliberate state-changing actions
- human-readable file states instead of exposing Git porcelain codes as the primary UI
- compact repository status chips for health, branch, and changed-item state
- a dedicated **Guardian Activity** console for command output
- dark hover help that explains actions in plain language
- a clean-project state instead of a large empty file table
- operation-only **Cancel**, visible only when something can actually be cancelled
- a subtle `GUARDIAN ONLINE` pulse
- native resizable Windows behavior with dark caption styling where Windows supports it

The application also ships with a canonical purple fox-head Windows icon used by the executable, taskbar, Guardian window, and system tray.

The pet itself follows the same visual language: its speech bubble is now a dark violet status surface, minimize is integrated into the bubble chrome, and Exit is a small hot-pink ribbon control on the fox rather than a detached Windows-style button.

## Meet the guardian

| Quiet sentinel | All checks green | Changes spotted | Watchful caution |
| --- | --- | --- | --- |
| <img src="mockups/pet/svg/pet_idle_01.svg" width="120" alt="Idle fox"> | <img src="mockups/pet/svg/pet_happy_01.svg" width="120" alt="Happy fox"> | <img src="mockups/pet/svg/pet_review_ready_01.svg" width="120" alt="Review-ready fox"> | <img src="mockups/pet/svg/pet_warning_01.svg" width="120" alt="Warning fox"> |
| Waiting / checking | Clean repository | Changes ready to review | Git needs attention |

Two additional approved mascot states, `pet_idle_02` and `pet_sleep_01`, remain reserved for later idle/sleep behavior.

## Designed for humans, not just Git experts

GitPet tries to explain *what will happen before it happens*.

- **Projects** remembers up to 20 recent repositories so you can switch quickly.
- **Open folder** accepts an existing Git repository immediately.
- **Prepare folder for Git** can safely turn an ordinary local folder into a Git repository with `git init -b main` after confirmation.
- **What Git should ignore** explains `.gitignore` as Git's “do not track these files” list instead of assuming the user already knows the term.
- **Before / After preview** shows the exact `.gitignore` content before anything is written.
- **Checkpoint** creates an ordinary local Git commit after showing the files and asking for confirmation.
- **Git Identity** appears automatically when Git does not yet know the commit author's name/email, with a safe project-only default and an optional PC-wide setting.
- **Push ↑** is always manual and shows the destination branch and commit before sending committed history to `origin`.
- **Help → About ZomniverseGitPet** shows the installed version, builder, build date, platform, MIT license, repository link, and copyright information.

Background repository checks run quietly; they do not take over the mouse cursor or present themselves as foreground work.

## Project workflow

```text
Choose or open a folder
        ↓
GitPet inspects it
        ↓
┌───────────────────────┬────────────────────────────┐
│ Existing Git project  │ Ordinary local folder      │
│ Open immediately      │ Offer safe Git preparation │
└───────────────────────┴────────────────────────────┘
        ↓
Review what Git should ignore
        ↓
See .gitignore BEFORE and AFTER
        ↓
Monitor → Review → Checkpoint → optional Push
```

When a folder is selected, GitPet classifies it before doing anything:

- **Ready** — an existing readable Git repository; GitPet opens it immediately.
- **Nested repository** — the folder is inside another Git repository; GitPet identifies the real root and offers to use it rather than silently creating nested Git metadata.
- **Can prepare** — an ordinary readable folder with no Git metadata; GitPet can initialize local Git only after confirmation.
- **Invalid/unavailable** — Git metadata is unreadable, Git is unavailable, or the folder cannot be accessed; GitPet explains the problem and does not overwrite anything.

## Safe project preparation

For a non-Git folder, GitPet can perform only:

```text
git init -b main
```

It then verifies the repository root and applies only the `.gitignore` rules you explicitly accepted.

Project preparation does **not** create a hosting repository, configure `origin`, stage files, create the first commit, or push anything. Those remain separate, deliberate user actions.

## Friendly `.gitignore` hygiene

A `.gitignore` file is simply Git's list of files and folders it should leave alone. GitPet samples the project tree and can recommend common candidates such as:

- `bin/`, `obj/`, `.vs/`, `.idea/`
- `node_modules/`, Python caches and virtual environments
- `.DS_Store`, `Thumbs.db`, `*.user`, `*.suo`, `*.log`
- `.env`, environment-specific secret files, `*.pem`, and `*.key`
- review-only candidates such as `dist/`, `coverage/`, `.cache/`, `tmp/`, and `temp/`

The review window uses plain-language labels:

- **PRIVACY** — likely secrets or private machine configuration
- **RECOMMENDED** — usually generated/cache/IDE material
- **CHECK FIRST** — may be generated, but some projects intentionally commit it

The right side of the review window shows the target `.gitignore` file in two read-only panels:

1. **CURRENT** — exactly what the file contains now
2. **AFTER** — exactly what the file would contain if the selected suggestions were applied

Nothing is written until the user approves the action. Existing `.gitignore` content is preserved and only accepted missing rules are appended.

Environment templates such as `.env.example`, `.env.sample`, `.env.template`, and `.env.dist` are deliberately protected from broad ignore suggestions because projects often need to commit those examples.

## Friendly Git identity setup

Git requires an author identity before it can create a commit. If a user tries to create a Checkpoint and Git does not yet know `user.name` and `user.email`, GitPet shows a normal form instead of exposing Git's raw terminal instructions.

The user enters:

- **Display name** — the author name stored in commit history
- **Email address** — the address stored in commit history; Git hosting noreply addresses are supported
- **This project only** — the default and safest option
- **All Git projects on this PC** — optional, explicit global configuration

GitPet explains that commit metadata can become public if a commit is later pushed. The privacy explanation remains available in a scrollable read-only panel, and the identity window itself is resizable within sensible limits.

## Core features

- Friendly purple fox desktop pet with repository-state visuals
- Canonical fox-head executable, taskbar, window, and tray icon
- Dark Guardian Console visual system
- Single-instance behavior; a second launch activates the existing Guardian
- Quiet background repository monitoring
- Recent-project registry with up to 20 repositories
- Folder suitability inspection and safe local Git initialization
- Nested-repository protection
- Reviewable `.gitignore` advisor with exact before/after preview
- Friendly first-time Git identity setup
- Human-readable changed-file states and diff review
- Configurable test hooks
- Recent commit history
- `git fsck` repository health checks
- Confirmed full-working-tree checkpoints using ordinary local Git commits
- Explicit, confirmed manual push to an existing `origin` remote on the current branch
- Suspicious-file safeguards and append-only local audit logging
- Built-in Help/About panel
- Typed per-user configuration under `%LOCALAPPDATA%\ZomniverseGitPet`

## Safety philosophy

A checkpoint previews all current non-ignored changes, asks for confirmation, ensures Git has an author identity, stages with `git add -A`, and creates a normal **local** Git commit.

The separate **Push ↑** action verifies that an existing `origin` remote and named current branch are available, shows the destination and latest commit, and asks for explicit confirmation before running the equivalent of:

```text
git push origin <current-branch>
```

Push sends committed history only. It does not stage or commit working-tree changes, create/configure remotes, force-push, reset, or clean. Automatic checkpoints are off by default and never trigger a push.

## Project status

ZomniverseGitPet 0.3 is an early public preview. The native C# application replaces the original PowerShell proof of concept, which remains in `prototype/powershell/` as a reference implementation.

Current platform support: Windows 10/11, x64, with Git for Windows available as `git.exe`.

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

## Local development build

Run `scripts/publish-local.ps1` to create the runnable self-contained executable in the sibling `ZomniverseGitPet_Releases/current` directory.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-local.ps1
```

Normal compiler output remains under the ignored `bin/` and `obj/` directories; the runnable local copy is outside the source repository.

## Configuration

ZomniverseGitPet writes its typed JSON configuration and append-only audit trail beneath `%LOCALAPPDATA%\ZomniverseGitPet`. The configuration stores the active repository and an MRU registry of up to 20 recent repositories; no machine path is built into the application or public source tree.

Test hooks are ordinary command strings run from the selected repository. Edit `config.json` while ZomniverseGitPet is closed, then restart it. Treat test commands as trusted local configuration.

## Roadmap

- Micro-animation and richer repository-state transitions
- Richer shared repository snapshot/state model across pet and Guardian
- Curated public screenshot/feature gallery using a safe demo repository
- In-app settings editor for tests and checkpoint policy
- File-system-assisted refresh with polling fallback
- Signed release artifacts and installer packaging
- Broader accessibility and multi-monitor refinements
- Task/worktree-aware checkpoints for parallel AI agents
- Additional automated integration and UI tests

## License

ZomniverseGitPet is available under the [MIT License](LICENSE).
