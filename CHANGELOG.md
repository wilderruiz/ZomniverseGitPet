# Changelog

## Unreleased

## 0.3.6 - 2026-09-09

- Reworked project preparation into a larger, resizable **two-row ignore builder**: detected project-specific suggestions on top and a reusable/custom ignore library below.
- Added organized **PRIVACY**, **GENERATED**, **SYSTEM**, **ARCHIVE**, and **CUSTOM** ignore categories with checkboxes and per-row hover explanations showing why an item is normally kept out of Git and the exact patterns that will be written.
- Added an environment-secret preset that ignores `.env` and `.env.*` recursively while explicitly preserving simple and nested `.example`, `.sample`, `.template`, and `.dist` environment templates.
- Added a review-only **Names containing LEGACY** preset for projects that retain historical copies beside live source.
- Added friendly custom ignore creation for folder names, file extensions, exact file names, and names containing text; GitPet converts these choices into `.gitignore` glob patterns instead of asking non-programmers to write Git patterns manually.
- Changed the CURRENT and AFTER preview area to a draggable horizontal safe split so each preview can receive substantially more vertical room; the detected/library area on the left is independently resizable too.
- Added explanatory comment blocks to generated root `.gitignore` sections so selective tracking scope, user-selected ignore patterns, recursive behavior, and safe `!` template exceptions remain understandable after the file is written.
- Unified `.gitignore` preview and apply through the same composer so the approved AFTER preview is the exact text GitPet writes.
- Hardened selective-scope ordering so internal rules such as `/CV/*` remain in the scope block before later hygiene patterns and cannot accidentally undo a selected deep-folder re-inclusion.
- Expanded lightweight regression coverage to **21 checks**, including preset translation, custom patterns, explanatory preview/apply equality, environment-template exceptions, and order-sensitive scope composition.
- Bumped the application version to **0.3.6**.

## 0.3.5 - 2026-09-09

- Added a Dropbox-style **project scope tree** before preparing a normal folder for Git, with files/folders selected by checkbox and expandable subfolders for narrower tracking scopes.
- Selective preparation now translates the approved tree into root `.gitignore` allow-list rules, so unselected content stays outside the new repository instead of merely disappearing from the UI.
- Existing nested Git repositories are detected, shown as protected/excluded in the scope picker, and explicitly ignored by the parent preparation scope rather than being silently absorbed.
- `.gitignore` hygiene is now scope-aware: selected subfolders are inspected in their own context and nested `.gitignore` files are discovered and shown read-only during review.
- Expanded the preparation review window vertically, giving substantially more room to the **PICK THE ITEMS**, **CURRENT IGNORE FILES**, and **AFTER** sections while keeping the window resizable.
- The CURRENT pane now shows the project-root `.gitignore` plus nested `.gitignore` files already in effect; only the root file is proposed for modification.
- Environment template names such as `.env.development.example`, `.env.*.sample`, `.env.*.template`, and `.env.*.dist` are filtered from privacy suggestions.
- Added selective-scope regression coverage for deep folder inclusion, sibling exclusion, nested-repository protection, and nested `.gitignore` awareness; the lightweight suite now contains 17 checks.
- Bumped the application version to **0.3.5**.

## 0.3.4 - 2026-09-09

- Reworked the lower Guardian area into a dual-mode **File Review / Guardian Activity** workspace.
- Clicking a changed file now opens a resizable side-by-side **BEFORE / NOW** comparison automatically; the **Diff** button opens the same review for the selected file.
- **BEFORE** reads the file from the latest local commit/checkpoint (`HEAD`) and **NOW** reads the current working-tree file without modifying either version.
- Added Zomniverse-style syntax highlighting for common JavaScript/TypeScript, C#, PHP, Python, SQL, PowerShell, JSON, HTML/XML/SVG, and CSS-family source files while preserving the exact source indentation and text.
- Zero-context Git diff ranges softly illuminate changed lines on both sides of the comparison.
- New files show that no baseline version exists; deleted files show that the working-tree version is gone.
- Repositories with no local commit/checkpoint yet show a clear **Create checkpoint** action so the user can establish a baseline for future Before / Now reviews.
- Guardian operations such as Tests, Checkpoint, Pull, Push, History, and Health switch back to **Guardian Activity**, while File Review includes an **Activity** button for manual return.
- Added read-only Git helpers for `HEAD` existence, `HEAD:<path>` file retrieval, and zero-context diff-against-HEAD review.
- Expanded the lightweight regression suite from 10 to 12 checks with diff-line mapping coverage, including new-file `0,0` ranges.
- Bumped the application version to **0.3.4**.

## 0.3.3 - 2026-09-09

- Added **per-project test profiles** so each remembered repository can keep its own test commands instead of sharing one global list.
- Pressing **Tests** on an unconfigured project now opens a branded **Project Tests** setup window instead of ending with a raw “no test commands configured” message.
- GitPet can suggest likely test commands from common project metadata such as `package.json`, `.sln`/`.csproj`, `pyproject.toml`, `composer.json`, and `Cargo.toml`; suggestions are review-only and are never executed merely because they were detected.
- Added **Save** and **Save & run tests** actions, plus **Shift + Tests** to reopen the editor for an already-configured project.
- Manual test runs execute commands from the active repository root in order, stop on the first failure, and show a PASS/FAIL summary in Guardian Activity.
- Automatic verified checkpoints now use the active repository's own test profile when tests are required.
- Existing legacy global test commands migrate once into the active project when upgrading older configuration.
- Added regression coverage for per-project test isolation and non-mutating test-command discovery.
- Bumped the application version to **0.3.3**.

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
