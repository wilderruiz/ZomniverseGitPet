# Commands reference

This page lists GitPet's fixed built-in external command families. In addition, a user can explicitly save arbitrary **Test Commands** for a project; those commands are user configuration and therefore cannot be exhaustively enumerated here. Reconcile validation runs saved Test Commands only after they have been configured for that project.

## Git

| Command | Where | Notes |
| --- | --- | --- |
| `git init -b main` | Preparing a new project | Only after the user approves scope + hygiene review. |
| `git status --porcelain=v2` / `git status` | Change detection, preflight, reconcile validation | Read-only. |
| `git diff`, `git log` | File Review, History | Read-only. |
| `git add -A -- <files>` | Save (normal files) | |
| `git add -f -- <exact-path>` | Save (explicitly approved ignored file) | Never a directory or glob. |
| `git add -- <exact-path>` | Reconcile LOCAL/candidate/isolated REMOTE staging | Exact inspected path. |
| `git commit -m "checkpoint: <timestamp>"` | Save | |
| `git commit -m "reconcile correction: <timestamp>"` | LOCAL correction / isolated REMOTE correction | New commit; no history rewrite. |
| `git for-each-ref --format=%(refname) refs/heads refs/remotes/origin` | Repository branch dropdown | Lists local and origin branches. |
| `git switch <branch>` | Repository branch dropdown | Working tree must be clean. |
| `git switch --track -c <branch> origin/<branch>` | Repository branch dropdown | Creates a local tracking branch for an online-only origin branch. |
| `git pull --ff-only origin <branch>` | Get | Never creates a merge commit. |
| `git push origin <branch>` | Send | Never force-pushed. |
| `git push origin <correction-sha>:refs/heads/<branch>` | Reconcile Inspector REMOTE Send | Normal non-force push; live tip is checked first. |
| `git fetch --quiet origin` | Background sync polling | At least every 10 seconds. |
| `git fetch --quiet origin <branch>:refs/remotes/origin/<branch>` | Refresh REMOTE correction tracking ref | After verified Send. |
| `git rev-list --left-right --count HEAD...origin/<branch>` | Computing ahead/behind | Read-only. |
| `git rev-parse ...` | Reconcile Inspector identity / merge-state checks | Pins HEAD, origin branch, and `MERGE_HEAD`. |
| `git merge-base <local> <remote>` | Reconcile Inspector | Finds BASE. |
| `git merge-base --is-ancestor <pinned> <correction>` | REMOTE Send | Verifies correction descends from inspected REMOTE. |
| `git cat-file -e <revision>:<path>` / `git cat-file -e <sha>^{commit}` | Reconcile Inspector | Read-only existence/object checks. |
| `git show <revision>:<path>` | Reconcile Inspector source loading | Read-only pinned source. |
| `git diff --unified=0 <base> <side> -- <path>` | Reconcile Inspector analysis | Read-only BASE hunk map. |
| `git merge-file -p <LOCAL> <BASE> <REMOTE>` | Merged preview | Runs against temporary files; does not touch repository state. |
| `git merge --no-commit --no-ff origin/<branch>` | Starting normal reconciliation | No automatic commit. |
| `git merge --no-commit --no-ff <remote-sha>` | Disposable candidate validation worktree | Isolated validation only. |
| `git checkout --ours -- <path>` / `--theirs` | Established whole-file conflict resolver | |
| `git rm -- <path>` | Established conflict resolver | When the chosen side deleted the file. |
| `git diff --name-only --diff-filter=U` | Reconciliation/candidate verification | Lists unresolved paths. |
| `git diff --cached --name-only` | LOCAL edit safety | Ensures exact-file commit boundary. |
| `git merge --abort` | Cancelling/failing reconciliation or candidate application | Restores pre-merge state. |
| `git commit -m "reconcile local and online: <timestamp>"` | Finishing reconciliation (via Save) | |
| `git ls-remote --heads origin refs/heads/<branch>` | REMOTE/candidate race checks | Reads live remote branch tip. |
| `git worktree add -b <temp-branch> <temp-path> <remote-sha>` | REMOTE correction preparation | Isolated correction workspace. |
| `git worktree add --detach <temp-path> <baseline-sha>` | Reconcile validation | Disposable validation workspace. |
| `git worktree remove --force <temp-path>` | REMOTE/validation cleanup | Removes GitPet's disposable worktree, not the primary tree. |
| `git worktree prune` | Validation cleanup | Removes stale disposable worktree metadata. |
| `git branch -D <gitpet-temp-branch>` | REMOTE correction cleanup | Deletes GitPet's temporary local branch only. |
| `git remote add origin <url>` | Connecting a remote | Refuses to overwrite an existing `origin`. |
| `git clone <url> <destination>` | Cloning | Destination must be empty first. |
| `git config --global --add safe.directory <exact-path>` | Dubious-ownership recovery | Exact path only, never a wildcard. |
| `git check-ignore -v` | Ignored-file detection | |
| `git ls-tree -r --full-tree --name-only HEAD -- <pathspecs>` | Allow-list resolution, publish-boundary comparison | Read-only. |
| `git fsck --no-progress` | Health check | |

## Reconcile validation commands

These commands run only inside the disposable validation workspace when the matching local executable is available:

| Command family | Purpose |
| --- | --- |
| `php -l <file>` | PHP syntax |
| `node --check <file>` | JavaScript syntax |
| `python -m py_compile <file>` | Python syntax |
| `powershell -NoProfile -NonInteractive -Command ...` | PowerShell parser-only validation |
| `where <executable>` | Checks whether an optional validator is installed |
| `cmd.exe /d /s /c <saved Test Command>` | Runs the active project's explicitly saved test commands in order |

JSON/XML/CSS deterministic checks are implemented in-process and therefore do not launch an external validator.

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

Runs an independent set of Git commands inside its isolated working copy. The local workspace may initialize with `git init -b main`, but the selected standalone remote branch drives remote operations:

- `git ls-remote --heads <standalone-remote>` — discover existing branches for the Branch selector.
- `git ls-remote --heads <standalone-remote> refs/heads/<selected-branch>` — probe the selected branch.
- `git fetch --prune origin <selected-branch>` — standalone Get/inspection only, never in the parent repository.
- `git diff ... refs/remotes/origin/<selected-branch>` and `git reset --hard refs/remotes/origin/<selected-branch>` — isolated workspace comparison/materialization.
- `git push -u origin HEAD:refs/heads/<selected-branch>` — standalone Send without force-push.

See [Publishing Architecture](../developer/PUBLISHING_ARCHITECTURE.md).
