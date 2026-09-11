# ADR-0002 — Model logical projects as scoped overlays on a shared repository

Status: Accepted
Date: 2026-09-11

## Context

Users sometimes want to treat a subfolder of a larger Git repository (a package in a monorepo, a personal tool nested in a larger workspace) as its own distinct, trackable thing inside GitPet — with its own name, its own saved test commands, and potentially its own publishing destination — without necessarily wanting a second, independent Git repository and history for it.

## Decision

A GitPet "project" (`RecentRepositoryEntry`) is a named record pointing at a repository root plus an optional scope (a set of selected relative paths, or "track everything"). Multiple project records can point at the same physical `RepositoryRoot`, each with independent scope, name, and per-project settings, all sharing the one underlying Git history and working tree.

## Reasons

- Avoids forcing a repository split (and the history-rewriting or submodule complexity that implies) just to get a separately-nameable, separately-scoped view of part of a folder.
- Lets a user register as many logical projects against one repository as they find useful, without GitPet needing to understand or manage nested repositories as a first-class case (those are explicitly detected and excluded from scope instead).
- Keeps ordinary whole-repository projects as the simple, default case (`TrackEverything = true`, no scope bookkeeping needed).

## Consequences

- Several logical projects sharing a repository also share its commit history — a Save in one is a commit on the same branch as any other logical project's Save, unless scope is enforced elsewhere in the workflow.
- Anything that needs "my project's files only" (ignore-rule generation, Send, the allow-list boundary check) has to consult the active project's resolved scope rather than assuming the whole working tree.
- Publishing a *scoped* project can't simply push the shared repository's `origin` without leaking the rest of the repository — this is why [ADR-0003](ADR-0003-STANDALONE-LOGICAL-PROJECT-PUBLISHING.md) exists.

## Alternatives considered

- **One Git repository per project**, using `git submodule` or a repository split — rejected: heavier for the common "just want to keep an eye on this subfolder separately" case, and submodules bring their own well-known usability problems.
- **Sparse-checkout as the scope mechanism** — rejected in favor of an explicit, GitPet-owned scope model that can be edited, saved, and compared (see [ADR-0004](ADR-0004-PROJECT-ALLOW-LIST-PUBLISH-BOUNDARY.md)) independently of what Git's own sparse-checkout state happens to be at any moment.
