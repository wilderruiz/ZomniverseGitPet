# ADR-0001 — Delegate GitHub authentication to the GitHub CLI

Status: Accepted
Date: 2026-09-11

## Context

GitPet needs to authenticate as a GitHub user to create repositories, discover existing ones, and (for maintainers) publish application releases. A desktop app that wants this typically implements an OAuth device/web flow itself and stores the resulting token somewhere on disk.

## Decision

GitPet implements no OAuth client and stores no personal access token. All GitHub authentication is delegated to the official GitHub CLI (`gh`): `GitHubAccountService` shells out to `gh --version`, `gh auth status`, `gh auth login`, `gh auth logout`, and `gh api`/`gh repo`/`gh release` subcommands, and derives current auth state fresh on each call rather than caching credentials itself.

## Reasons

- Removes an entire class of credential-storage risk from GitPet's own codebase — there is no token file, no keychain entry, no encryption scheme to get right or leak.
- `gh` already implements a maintained, correct OAuth device/web flow and its own secure credential storage; GitPet doesn't need to duplicate or keep it current.
- Sign-in/sign-out/account-switching become thin wrappers over an existing, well-tested tool instead of new surface area.

## Consequences

- GitHub-connected features require the GitHub CLI to be installed (GitPet can install it on request, but cannot function without it or an equivalent).
- GitPet has no visibility into or control over how `gh` stores its credentials — it can only ask "am I currently authenticated, and as whom?"
- Every GitHub-facing action becomes a process invocation, with the usual costs (process startup latency, parsing stdout/stderr) rather than a direct API call.

## Alternatives considered

- **Implement OAuth directly** (device flow or PKCE) with GitPet's own token storage — rejected: more code, more risk, and a second thing to keep working as GitHub's auth requirements evolve.
- **Require a manually-created personal access token pasted into GitPet** — rejected: worse UX and still requires GitPet to store and protect a long-lived secret.
