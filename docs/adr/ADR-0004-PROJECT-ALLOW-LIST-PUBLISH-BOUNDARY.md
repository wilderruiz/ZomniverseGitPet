# ADR-0004 — Treat the Advanced Project Allow List as a publishing contract

Status: Accepted
Date: 2026-09-11

## Context

A logical project's scope (see [ADR-0002](ADR-0002-LOGICAL-PROJECTS-AS-SCOPED-OVERLAYS.md)) can change over time as folders are added or removed. Without an independent check, a scope change (accidental or intentional) could cause a standalone publish to suddenly include files the user never meant to share, with no warning.

## Decision

A user can save a separate, plain-text **Advanced Project Allow List** describing what the project is *supposed to* publish. This is stored independently from the live scope selection. Before every standalone publish, if an allow list is saved, GitPet resolves it against the repository's committed tree and compares it to the live send snapshot (`ProjectPublishBoundary.CompareAsync`), and blocks the Send unless the two match exactly.

## Reasons

- Separating "what I intend to publish" (the allow list, edited deliberately and rarely) from "what's currently selected" (the scope tree, which can drift) creates a second, independent check rather than trusting one single source of truth.
- An exact-match requirement (no extra files, no missing files, no unresolved allow-list entries) means any drift is caught before it's sent, not after.
- Making the allow list optional (no check at all if none is saved) avoids forcing this extra step on projects that don't need it.

## Consequences

- A saved allow list must be kept in sync with real scope changes, or the project simply can't Send until it's updated — this is a deliberate friction, not an oversight.
- Every standalone publish does at least one extra `git ls-tree` read before proceeding, which is cheap but not free.
- The comparison is necessarily a snapshot at Send time — it can't catch a scope change made after the comparison but before the push completes (a narrow window, given the whole flow runs as one user-initiated action).

## Alternatives considered

- **Trust the live scope selection alone, no separate contract** — rejected: gives no independent signal when scope silently changes; the whole point of this feature is a second, deliberately-maintained check.
- **Warn instead of block on mismatch** — rejected: a warning that's easy to dismiss doesn't meet the goal of preventing scope leakage; a hard block forces the user to look at exactly what differs before anything is sent.
