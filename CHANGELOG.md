# Changelog

## Unreleased

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
- Improved visible Guardian/pet refresh coordination while the Guardian is open.
- Expanded the lightweight regression suite for recent-project registration and `.gitignore` recommendation behavior.

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
