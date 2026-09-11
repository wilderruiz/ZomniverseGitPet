# Getting started

GitPet is a small desktop companion that watches over one or more local folders that are (or become) Git repositories, and gives you plain-language buttons for the handful of Git actions you actually need day to day.

## First run

The first time GitPet runs, it walks you through a short setup before anything else opens:

1. **Choose how GitPet talks to GitHub.**
   - **GitHub-connected mode** — GitPet installs (if needed) and signs you in through the official [GitHub CLI](https://cli.github.com/) (`gh`). GitPet never asks for or stores a personal access token itself; it relies entirely on `gh`'s own sign-in and credential storage.
   - **Local Git Only mode** — nothing about your work leaves this PC through GitPet. You still get Review, Save, History, Tests, and repository health; you just won't see GitHub-specific actions like creating a repository. You can switch to GitHub-connected mode later at any time, and switching back to Local Git Only never deletes a project, a commit, a remote, a file, or a GitHub repository — it only changes which buttons GitPet shows you.
2. **Choose a project to start with**, one of:
   - **Open an existing project** — pick a folder. If it's already a Git repository, GitPet opens it directly; if it isn't, GitPet offers to prepare it (see [Projects](PROJECTS.md)).
   - **Clone an online project** — paste a repository address and a destination folder; GitPet clones it and then walks you through the same project-scope review as a freshly prepared folder.
   - **Start without a project** — finish setup with nothing open yet, and open or clone a project later from the toolbar.

Completing any of these paths marks first-run setup as done; GitPet won't show it again unless its own settings are reset.

## What you'll see day to day

Once a project is open, the **Guardian** window is the main surface:

- A header showing the branch name and whether the repository is healthy.
- A list of changed files, colored by what changed.
- The **File Review** pane, a before/now comparison for whichever file you click.
- A toolbar: **Projects · Refresh · Review · Tests · Save · Get ↓ · Send ↑ · History · Health**.
- A quiet activity log of what GitPet has done.

Alongside Guardian, a small fox stays near your desktop as a lightweight status indicator (idle, healthy, changes-ready-for-review, or needs-attention) — it's cosmetic, not a second place to take action.

## The next things to read

- [Save, Get, Send](SAVE_GET_SEND.md) — the loop you'll use constantly.
- [Projects](PROJECTS.md) — opening, preparing, and scoping a project.
- [GitHub Connection](GITHUB_CONNECTION.md) — connecting a repository to GitHub, or staying local-only.
