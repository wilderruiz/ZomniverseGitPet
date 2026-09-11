# ADR-0003 — Publish scoped logical projects via an isolated workspace, not subtree/sparse-checkout

Status: Accepted
Date: 2026-09-11

## Context

Given [ADR-0002](ADR-0002-LOGICAL-PROJECTS-AS-SCOPED-OVERLAYS.md), a scoped logical project shares its parent repository's history and `origin`. Sending such a project needs to publish only its own files, to its own destination, without ever pushing the parent repository's full history or its `origin` remote as a side effect.

## Decision

`StandaloneProjectPublishing` builds and maintains a completely separate, on-disk Git working copy per logical project (under `%LOCALAPPDATA%\ZomniverseGitPet\Publishing\<projectId>\`), containing only that project's in-scope files copied out of the parent repository's committed tree, with its own `.git` history and its own linked remote. Publishing recreates the workspace's tracked content from a fresh snapshot each time, commits only if the content actually changed (via a content-based fingerprint), and pushes from there.

## Reasons

- Structurally impossible to push the parent repository by mistake — the workspace has no relationship to the parent's `origin` at all, and lives outside it on disk.
- The published history is exactly what the project's own remote should show: only its files, with commit messages describing that project's publishes, not interleaved with the parent repository's unrelated history.
- A content-based fingerprint (hashing the resolved pathspecs plus the raw `git ls-tree` output, so it changes even when only a file's *content* changes at the same path) means "already published" is a real, safe check rather than a heuristic that could hide a real change.

## Consequences

- Requires disk space and a bit of extra work (copying files, force-adding, committing) on every publish rather than a lighter-weight `git push` of an existing ref.
- The workspace is disposable and rebuilt from the parent repository's tree each time — it should never be treated as a place to make manual edits.
- A scoped project's published history has no relationship to the parent repository's commit graph; anyone consuming the published remote sees a fresh, project-only history rather than a filtered view of the original one.

## Alternatives considered

- **`git subtree push`** — rejected: splits history from the parent repository's actual commits, which drags along parent-repository commit metadata/authorship not relevant to the standalone project, and is comparatively fragile to script reliably.
- **Sparse-checkout of a second working tree against the same `origin`** — rejected: still shares the same remote and history namespace as the parent repository, defeating the goal of never being able to push the wrong thing.
- **`git filter-branch` / `git filter-repo` to rewrite a filtered copy on each publish** — rejected: heavier, slower, and rewrites history each time rather than building forward from a stable, incremental log of standalone-project commits.
