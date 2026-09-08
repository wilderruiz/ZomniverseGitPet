<p align="center">
  <img src="mockups/pet/svg/pet_happy_01.svg" width="180" alt="ZomniverseGitPet purple fox guardian">
</p>

<h1 align="center">ZomniverseGitPet</h1>

<p align="center"><strong>A desktop Git guardian for human and AI-assisted development.</strong></p>

ZomniverseGitPet is a lightweight Windows desktop companion that keeps a small purple fox near your workspace and turns Git safety into a visible, low-friction habit. It is designed for experienced developers, people working with AI coding agents, and users who do not want to memorize Git commands just to keep their projects safe.

Double-click the pet to open the Guardian Console. GitPet can watch and switch between projects, inspect ordinary folders before Git setup, safely initialize local Git metadata, explain and preview `.gitignore` changes, review changed files and diffs, configure and run project-specific tests, create local checkpoints, explicitly connect an existing remote repository, manually pull remote updates, manually push committed history, display recent commits, and run Git health checks.

GitPet never pulls or pushes automatically, never invents or creates online repositories automatically, never replaces an existing remote automatically, and never uses destructive operations such as `reset --hard` or `clean`.

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
- **Tests** stores a separate test profile for each project; if none exists, GitPet suggests likely commands for review and lets the user Save or Save & run tests.
- **Checkpoint** creates an ordinary local Git commit after showing the files and asking for confirmation.
- **Git Identity** appears automatically when Git does not yet know the commit author's name/email, with a safe project-only default and an optional PC-wide setting.
- **Connect Remote** appears when a remote action needs `origin`; the user pastes the clone URL of an existing online repository and explicitly approves the connection.
- **Pull ↓** is always manual, requires a clean working tree, and uses fast-forward-only safety so GitPet never creates an automatic merge commit.
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
Configure project tests if useful
        ↓
Monitor → Review → Test → Checkpoint
        ↓
Pull remote updates ↓   /   Push local commits ↑
        ↓
If no origin exists: paste an existing remote clone URL
        ↓
Confirm the requested Pull or Push
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

Project preparation does **not** create a hosting repository, configure `origin`, stage files, create the first commit, pull, or push anything. Those remain separate, deliberate user actions.

## Per-project tests

Each remembered repository can keep its own local test profile. Test commands are stored in GitPet's per-user configuration, not written into the repository itself.

When **Tests** is pressed on a project that has no saved test commands, GitPet opens a **Project Tests** window. It can suggest likely commands from common project metadata such as:

- `package.json` test scripts → `npm test` / `npm run test:*`
- `.sln` / `.csproj` → `dotnet test`
- `pyproject.toml` / `pytest.ini` / `tox.ini` → `python -m pytest`
- `composer.json` test script → `composer test`
- `Cargo.toml` → `cargo test`

Suggestions are advisory only. Discovery does not edit files and does not execute anything. The user reviews the commands, one per line, and chooses **Save** or **Save & run tests**.

Manual test runs:

1. run from the active repository root
2. execute commands in the saved order
3. stop on the first failure
4. show each command plus PASS/FAIL in **Guardian Activity**

Hold **Shift** while clicking **Tests** to reopen the editor for an already-configured project.

If automatic verified checkpoints are explicitly enabled and configured to require tests, GitPet uses the active project's own saved test profile before creating an automatic checkpoint. Test commands are treated as trusted local commands.

## Explicit remote connection

A local Git repository and an online Git repository are separate things. Git identity (`user.name` / `user.email`) is enough to create local commits, but Pull and Push also need a destination/source remote.

If the user starts **Pull ↓** or **Push ↑** and no readable `origin` exists, GitPet opens a **Connect Remote** window. The user pastes the clone URL of an already-existing repository, for example:

```text
https://github.com/user/project.git
```

or:

```text
git@github.com:user/project.git
```

The same flow works with GitLab, Bitbucket, private Git servers, HTTPS, SSH, and other Git-compatible remote addresses.

After explicit approval GitPet performs the equivalent of:

```text
git remote add origin <user-provided-url>
```

GitPet then returns to the Pull or Push action that the user started. It does **not** create the online repository, does not replace an existing `origin`, does not stage or commit anything, and does not pull or push merely because the remote was connected. The remote URL itself is not copied into GitPet's audit log.

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
- Explicit, user-approved `origin` connection for an existing remote repository
- Human-readable changed-file states and diff review
- Per-project test profiles with reviewable command discovery and sequential PASS/FAIL output
- Recent commit history
- `git fsck` repository health checks
- Confirmed full-working-tree checkpoints using ordinary local Git commits
- Explicit, confirmed **Pull ↓** from `origin` on the current branch with clean-tree and fast-forward-only safeguards
- Explicit, confirmed **Push ↑** to `origin` on the current branch
- Suspicious-file safeguards and append-only local audit logging
- Built-in Help/About panel
- Typed per-user configuration under `%LOCALAPPDATA%\ZomniverseGitPet`

## Safety philosophy

A checkpoint previews all current non-ignored changes, asks for confirmation, ensures Git has an author identity, stages with `git add -A`, and creates a normal **local** Git commit.

Remote connection is a separate explicit action. GitPet only adds `origin` after the user supplies the clone URL and approves it, and it refuses to overwrite an existing `origin` automatically.

**Pull ↓** refuses to start if the working tree has uncommitted changes. After confirmation it runs the equivalent of:

```text
git pull --ff-only origin <current-branch>
```

Fast-forward-only means GitPet never creates an automatic merge commit. If local and remote histories have diverged, Pull stops and leaves the history for the user to review manually.

**Push ↑** shows the destination and latest commit and asks for explicit confirmation before running the equivalent of:

```text
git push origin <current-branch>
```

Push sends committed history only. It does not stage or commit working-tree changes, create an online repository, force-push, reset, or clean. Automatic checkpoints are off by default and never trigger Pull or Push.

## Project status

ZomniverseGitPet **0.3.3** is an early public preview. The native C# application replaces the original PowerShell proof of concept, which remains in `prototype/powershell/` as a reference implementation.

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

ZomniverseGitPet writes its typed JSON configuration and append-only audit trail beneath `%LOCALAPPDATA%\ZomniverseGitPet`. The configuration stores the active repository, an MRU registry of up to 20 recent repositories, and each project's local test profile; no machine path is built into the application or public source tree.

Test profiles can be configured directly from **Tests** in Guardian. Treat saved test commands as trusted local configuration.

## Roadmap

- Micro-animation and richer repository-state transitions
- Richer shared repository snapshot/state model across pet and Guardian
- Curated public screenshot/feature gallery using a safe demo repository
- In-app settings editor for checkpoint policy
- File-system-assisted refresh with polling fallback
- Signed release artifacts and installer packaging
- Broader accessibility and multi-monitor refinements
- Task/worktree-aware checkpoints for parallel AI agents
- Additional automated integration and UI tests

## License

ZomniverseGitPet is available under the [MIT License](LICENSE).
