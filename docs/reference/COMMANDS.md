# Commands reference

The exact external commands GitPet runs, and where in the app each one comes from. GitPet never runs a Git or GitHub CLI command outside of this list as part of its built-in workflows.

## Git

| Command | Where | Notes |
| --- | --- | --- |
| `git init -b main` | Preparing a new project | Only after the user approves scope + hygiene review. |
| `git status --porcelain=v2` / `git status` | Change detection, preflight checks | Read-only. |
| `git diff`, `git log` | File Review, History | Read-only. |
| `git add -A -- <files>` | Save (normal files) | |
| `git add -f -- <exact-path>` | Save (explicitly approved ignored file) | Never a directory or glob. |
| `git commit -m "checkpoint: <timestamp>"` | Save | |
| `git pull --ff-only origin <branch>` | Get | Never creates a merge commit. |
| `git push origin <branch>` | Send | Never force-pushed. |
| `git fetch --quiet origin` | Background sync polling | At least every 10 seconds. |
| `git rev-list --left-right --count HEAD...origin/<branch>` | Computing ahead/behind | |
| `git merge --no-commit --no-ff origin/<branch>` | Starting reconciliation | |
| `git checkout --ours -- <path>` / `--theirs` | Resolving a reconciliation conflict | |
| `git merge --abort` | Cancelling or failing a reconciliation | |
| `git commit -m "reconcile local and online: <timestamp>"` | Finishing reconciliation (via Save) | |
| `git remote add origin <url>` | Connecting a remote | Refuses to overwrite an existing `origin`. |
| `git clone <url> <destination>` | Cloning | Destination must be empty first. |
| `git config --global --add safe.directory <exact-path>` | Dubious-ownership recovery | Exact path only, never a wildcard. |
| `git check-ignore -v` | Ignored-file detection | |
| `git ls-tree -r --full-tree --name-only HEAD -- <pathspecs>` | Allow-list resolution, publish-boundary comparison | Read-only. |
| `git fsck --no-progress` | Health check | |

## GitHub CLI (`gh`)

| Command | Where |
| --- | --- |
| `gh --version`, `gh auth status`, `gh api user --jq .login` | Checking current auth state |
| `gh auth login --web --git-protocol https`, `gh auth setup-git` | Signing in |
| `gh auth logout [--user <login>]` | Signing out / switching accounts |
| `gh repo create <owner>/<name> --private\|--public [--description ...]` | Creating a GitHub repository |
| `gh repo list <login> --json nameWithOwner,url,updatedAt` | Finding candidate repositories |
| `gh release view <tag>` | Checking a release doesn't already exist, before publishing GitPet's own release |
| `gh release create <tag> <installer> <portable> <manifest> <checksums> --repo ... --title ... --notes-file ... --latest` | Publishing a GitPet application release (maintainer-only) |

## Isolated publishing workspace (standalone logical-project publishing)

Runs an independent set of the same Git commands (`init -b main`, `add -f -A`, `commit`, `push -u origin main`) inside its own isolated working copy — see [Publishing Architecture](../developer/PUBLISHING_ARCHITECTURE.md).
