# Changelog

## Unreleased

## 0.3.2 - 2026-09-09

- Added a manual **Pull ↓** action beside **Push ↑** in the Guardian Console.
- Pull always targets the current named branch and existing `origin`; it is never automatic.
- Pull refuses to run while the working tree has uncommitted changes and asks the user to create a Checkpoint first.
- Pull uses `git pull --ff-only origin <current-branch>`, so GitPet never creates an automatic merge commit. Diverged histories stop safely for manual review.
- Added a cool blue/violet Pull button treatment to visually contrast with the hot-pink Push action.
- Generalized **Connect Remote** wording so the same explicit `origin` setup can be used safely from either Pull or Push.
- Bumped the application version to **0.3.2**.

## 0.3.1 - 2026-09-09

- Added a friendly **Connect Remote** step when the user presses **Push ↑** and the current project has no `origin` remote.
- The user pastes the clone URL of an already-existing GitHub, GitLab, Bitbucket, private-server, HTTPS, SSH, or other Git repository and explicitly confirms the connection.
- GitPet adds only `origin` and then continues to the normal manual Push confirmation; it does not create an online repository, replace an existing remote, stage files, commit, or push automatically.
- Remote URLs are not written to GitPet's audit log; the audit records only whether explicit remote configuration succeeded.
- Bumped the application version to **0.3.1**.

## 0.3.0 - 2026-09-09

- Introduced the dark **Guardian Console** visual system with graphite surfaces, violet structure, hot-pink action accents, dark tooltips, status chips, and a dedicated Guardian Activity console.
- Replaced raw Git status codes in the main file list with human-readable states while preserving the underlying porcelain codes in hover help.
- Added a clean-repository empty state and a subtle `GUARDIAN ONLINE` pulse without changing repository polling behavior.
- Changed **Cancel** into an operation-only action that appears only while a cancellable task is running.
- Added Windows dark-caption integration where supported while preserving normal resizable Windows window behavior.
- Added the canonical purple fox-head application icon for the executable, taskbar, Guardian window, and tray icon.
- Integrated pet chrome into the mascot: minimize now lives in the speech-bubble chrome and Exit is a hot-pink ribbon control on the fox.
- Restyled the pet speech bubble as a dark violet status surface and made pet messages more explicit (`CLEAN`, `CHANGES DETECTED`, `GIT NEEDS ATTENTION`).
- Bumped the public application version to **0.3.0**.
- Added Guardian dashboard hover tooltips for repository status and actions.
- Added the approved purple fox desktop-pet states to the native launcher.
- Added an explicit manual **Push** action for the existing `origin` remote and current branch, with destination preview and confirmation.
- Manual push sends committed history only and never creates/configures remotes or runs automatically.
- Added a recent-project registry that remembers up to 20 repositories and makes project switching available from the project chooser.
- Added folder suitability inspection with ready, nested-repository, preparable, invalid, Git-unavailable, and unavailable states.
- Added safe local preparation of ordinary folders with `git init -b main`; preparation never creates a remote, stages files, commits, or pushes.
- Added nested-repository protection that offers the existing repository root rather than silently creating nested Git metadata.
- Added reviewable `.gitignore` recommendations for common generated files, IDE state, caches, logs, environment files, and key material.
- `.gitignore` recommendations require explicit selection and acceptance; existing file content is preserved and only missing accepted rules are appended.
- Added a repository-hygiene command for reviewing `.gitignore` suggestions on existing projects.
- Reworked `.gitignore` review for non-programmers with plain-language labels and an exact read-only **CURRENT / AFTER** file preview before changes are applied.
- Made Guardian background polling silent so routine repository monitoring no longer repeatedly displays the Windows wait cursor or overwrites the operation panel with refresh messages.
- Added friendly Git identity setup when a Restore Point needs `user.name`/`user.email`, with project-only scope as the default and an explicit optional global scope.
- Added **Help → About ZomniverseGitPet** with version, builder, build date, platform, license, repository, and copyright information.
- Expanded the public README into a human-first product page and expanded regression coverage for project registration and `.gitignore` preview behavior.

## 0.2.0 - 2026-09-08

- Rebuilt ZomniverseGitPet as a native .NET 8 WinForms application.
- Added asynchronous Git operations, timeouts, cancellation, and responsive startup.
- Added per-user single-instance enforcement with existing-instance activation.
- Added system tray integration and corrected Guardian window lifecycle.
- Added generic repository selection and removed private prototype defaults.
- Preserved a sanitized PowerShell reference implementation.
- Added public documentation, MIT licensing, build metadata, and tests.

## 0.1.x

- Initial PowerShell proof of concept.
