# Safety model

This page states plainly what GitPet may do on its own, what it always asks for first, and what it refuses to do at all. It is deliberately unemotional — these are facts about the code, not reassurances.

## What GitPet may do without asking

| Action | Notes |
| --- | --- |
| Read repository status, history, and diffs | Never mutates anything. |
| Poll the remote (`git fetch`) to detect divergence | At least every 10 seconds; read-only. |
| Show File Review comparisons | Read-only. |
| Run project-configured tests | Only ones you saved yourself; only when you press Tests, or as part of an opt-in automatic checkpoint. |

## What GitPet always asks for first

| Action | Confirmation required |
| --- | --- |
| Save (local commit) | Preview + confirm; ignored files need per-file approval to be force-tracked. |
| Get (`pull --ff-only`) | Confirm; requires a clean tree; stops (offers Reconcile) rather than merging on divergence. |
| Send (`push`) | Confirm; only ever sends already-saved commits. |
| Reconcile | Confirm to start; per-file choice for any conflicts; a separate Save to finalize. |
| Adding a `safe.directory` entry | Explicit consent dialog, exact path only. |
| Connecting/creating a GitHub remote | Explicit form; never replaces an existing `origin`. |
| Publishing a scoped logical project | Blocked automatically on an allow-list mismatch; see [Project Boundaries](PROJECT_BOUNDARIES.md). |
| Installing a GitPet update | Shown release notes first; download is hash-verified before install. |
| Publishing a new GitPet release (maintainer-only) | Blocked automatically on provenance mismatch; explicit confirmation dialog otherwise. |

## What GitPet refuses to do

- Force-push any branch.
- Use `safe.directory=*` (a wildcard trust of every folder) — only the exact folder path is ever added.
- Run `git reset --hard` or `git clean` as part of any built-in workflow.
- Create a merge commit during Get (`--ff-only` only; divergence goes to Reconciliation instead).
- Overwrite an existing GitHub Release tag.
- Self-update a DEV or portable build (only an installed build checks for and applies updates).
- Publish a scoped logical project's parent repository as a side effect of publishing that project (see [Project Boundaries](PROJECT_BOUNDARIES.md)).

## Automatic checkpoints (opt-in)

Automatic Saving is **off by default**. When turned on, it still only ever creates local commits — it never triggers Get or Send, and can be configured to require project tests to pass first and to wait for a quiet period with no activity before committing.

## Related documents

- [Project Boundaries](PROJECT_BOUNDARIES.md)
- [Send Preflight](SEND_PREFLIGHT.md)
- [Reconciliation Safety](RECONCILIATION_SAFETY.md)
- [Filesystem and Sandbox](FILESYSTEM_AND_SANDBOX.md)
