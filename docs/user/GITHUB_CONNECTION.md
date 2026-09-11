# GitHub connection

GitPet can work entirely offline (Local Git Only mode) or connect to GitHub for account-aware repository creation and discovery. Either way, your local Git operations — Save, History, Tests, repository Health — work identically; the mode only changes which GitHub-specific actions are visible.

## GitHub-connected mode vs. Local Git Only

| | GitHub-connected | Local Git Only |
| --- | --- | --- |
| Save / Review / History / Tests / Health | Yes | Yes |
| Get / Send to an existing `origin` | Yes | Yes (origin just has to already be configured) |
| Create or discover a GitHub repository from inside GitPet | Yes | No — use [Connecting an existing remote](#connecting-an-existing-remote) instead |
| Account sign-in | Via the GitHub CLI | Not applicable |

Switching modes never deletes a project, a commit, a remote, a file, or a GitHub repository — it only changes GitPet's own UI.

## Authentication

GitPet does not implement its own OAuth flow and does not store a personal access token. All GitHub authentication is delegated to the official [GitHub CLI](https://cli.github.com/) (`gh`):

- If `gh` isn't installed, GitPet can install it for you.
- Signing in opens a normal browser-based GitHub login through `gh auth login`.
- Your credentials live wherever `gh` itself stores them — GitPet only ever asks `gh` "am I currently signed in, and as whom?"
- Changing accounts signs the current one out and starts sign-in again for the next one.

## Connecting an existing remote

If a repository has no `origin` configured, GitPet asks for one of:

- **It already exists on GitHub** — paste the address (`owner/repo`, an HTTPS URL, or an SSH URL) and GitPet wires it up as `origin`. Nothing is pushed as a result of connecting.
- **Create it for me** — pick a name, public or private, and GitPet creates the repository on GitHub and connects it. It creates the repository only — it doesn't push.
- **I'm not sure** — GitPet lists your GitHub repositories and ranks the ones whose name looks like a match for the local folder.
- **A plain "connect remote" form** is also available for any Git host, GitHub or not — you just paste the clone URL.

## Related documents

- [Logical Projects](LOGICAL_PROJECTS.md) and [Publishing Architecture](../developer/PUBLISHING_ARCHITECTURE.md) — a scoped project can be connected to its *own*, separate remote, distinct from the parent repository's `origin`.
