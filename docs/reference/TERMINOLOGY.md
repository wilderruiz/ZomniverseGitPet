# Terminology

GitPet's own vocabulary, mapped to the underlying Git concept it stands for. Use these terms consistently across all documentation.

| GitPet term | Underlying Git concept | Notes |
| --- | --- | --- |
| **Save** | `git commit` (local only) | Never pushes. |
| **Get ↓** | `git pull --ff-only` | Never creates a merge commit; stops on divergence. |
| **Send ↑** | `git push` | Only already-saved commits; never force-pushed. |
| **Reconcile** | Resolving diverged history via `git merge --no-commit --no-ff`, then Save | Never a silent/automatic merge commit. |
| **Checkpoint** | The commit message convention (`checkpoint: <timestamp>`) GitPet's own Save/automatic-Save actions use | Not a distinct Git feature — an ordinary commit. |
| **Project** | A `RecentRepositoryEntry` — GitPet's own record of a folder it watches | See [Project Model](../developer/PROJECT_MODEL.md). |
| **Logical project** | A project whose scope is smaller than its repository's root, sharing history with other logical projects in the same repository | See [Logical Projects](../user/LOGICAL_PROJECTS.md). |
| **Repository root** | The actual `.git` repository root on disk | May differ from a logical project's own root. |
| **Advanced Project Allow List** | A saved, plain-text description of a logical project's intended scope, used as a publishing contract | See [Advanced Project Allow Lists](../user/ALLOW_LISTS.md). |
| **Publish boundary / Compare with Send** | The diff between the allow list's resolved files and the live send snapshot | See [Project Boundaries](../safety/PROJECT_BOUNDARIES.md). |
| **Standalone publishing** | Pushing a scoped logical project's own isolated Git history to its own remote | See [Publishing Architecture](../developer/PUBLISHING_ARCHITECTURE.md). |
| **Guardian** | The main GitPet window (`GuardianForm`) | |
| **Guardian workboard** | The four-quadrant SAVE / GET / SEND / RECONCILE view | |
| **Milestone / Major Update** | An advisory feature that inspects the *user's own project* for signs of a big redesign and can create a legacy branch + tag | Unrelated to GitPet's own releases — see [Update System](../developer/UPDATE_SYSTEM.md). |
| **Application release** | Publishing a new version of *GitPet itself* to GitHub Releases (maintainer-only) | Unrelated to a user project's Milestones — see [Build and Release](../developer/BUILD_AND_RELEASE.md). |
| **DEV build** | An executable filename-prefixed `DEV-`, produced by `publish-local.ps1` | Never self-updates. |
| **Installed build** | A build installed via the Setup installer (detected by a sibling `unins000.exe`) | Only build type that self-updates. |
| **Portable build** | The self-contained single-file executable, run without installing | Never self-updates. |
