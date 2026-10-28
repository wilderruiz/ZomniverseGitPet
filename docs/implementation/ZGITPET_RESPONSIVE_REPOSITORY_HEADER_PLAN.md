# ZGit Pet responsive repository header

**Date:** 2026-10-28  
**Status:** PATCH READY — LOCAL APPLY + VISUAL SMOKE PENDING

## Problem

Guardian's repository header currently gives the status card a fixed 410 px column and Chat Vitality a fixed 428 px column. The repository identity row (`Watching this repository` + the GitHub URL) lives inside the remaining summary column.

At narrower supported window widths this reverses the desired information priority: fixed secondary panels keep their width while the active repository identity is squeezed first.

The repository being controlled is primary safety/context information and must remain visible at every supported Guardian width.

## UI contract

### Persistent repository strip

Move the existing repository identity controls into a dedicated full-width strip at the bottom of the repository card:

`Watching this repository  [copy]  github.com/owner/repository`

The strip:

- remains visible at every supported Guardian window width;
- keeps the full URL as the underlying link and tooltip;
- may visually ellipsize the URL when space is genuinely exhausted;
- remains clickable/copyable;
- does not disappear behind a menu or secondary panel;
- visually separates repository identity from the action toolbar below.

### Adaptive top context row

Keep the existing desktop identity rather than redesigning it:

- current project / latest checkpoint;
- Chat Vitality;
- Guardian / Health / Branch / Working Tree status.

Replace the hard-coded 428 px Chat Vitality and 410 px status widths with responsive sizing driven by the live repository-card width.

Target behavior:

1. **Wide** — preserve approximately the current roomy proportions.
2. **Medium** — Chat Vitality and status card shrink while project/checkpoint context retains usable width.
3. **Minimum supported window (920 px)** — all three context areas remain usable and the repository strip remains fully present beneath them.

The project pill keeps its existing live width cap and ellipsis behavior.

## Implementation shape

Primary file:

- `src/ZomniverseGitPet/GuardianForm.cs`

Prepared patch support:

- `scripts/apply-responsive-repository-header-2026-10-28.ps1`
- `scripts/validate-responsive-repository-header-2026-10-28.ps1`

The applicator is guarded and idempotent. It applies only when the expected current header anchors are present.

Expected source changes:

- make the repository card contain a two-row outer layout;
- place the current project/chat/status composition in row 0;
- place `repositoryLinkRow` in row 1 spanning the full card width;
- remove the repository row from the summary table;
- resize summary row definitions accordingly;
- replace the fixed Chat Vitality minimum with a compressible 280 px floor;
- add a live resize helper that scales Chat Vitality and status-card widths down to practical floors;
- allow the repository URL label to ellipsize visually while preserving the full target URL.

No Git behavior, repository selection behavior, Chat Vitality data, branch controls, Save/Get/Send/Reconcile behavior, or network behavior changes in this phase.

## Responsive sizing guardrails

The adaptive calculation reserves practical minimums rather than allowing one panel to consume all width.

Prepared floors:

- project/checkpoint area: protected by the remaining flexible column;
- Chat Vitality: 280 px;
- status card: 300 px.

At wide sizes the layout returns to the existing 428 px Chat Vitality / 410 px status card proportions.

Do not make the form minimum width larger merely to preserve the old fixed layout.

## Regression gates

- `powershell -ExecutionPolicy Bypass -File scripts/validate-responsive-repository-header-2026-10-28.ps1` passes after apply.
- `dotnet build ZomniverseGitPet.sln -c Release` passes.
- Existing test suite passes.
- `git diff --check` passes.
- Structural regression confirms:
  - repository identity row is no longer inside the summary table;
  - repository strip spans the full repository card width;
  - Chat Vitality can compress to its 280 px floor;
  - status width adapts down to its 300 px floor;
  - repository link supports visual ellipsis while retaining the full URL value.

## Manual visual checks

Check Guardian at approximately:

- 1180 px wide (current default);
- ~1024 px wide;
- 920 px minimum width.

At all three widths verify:

- current project remains identifiable;
- Chat Vitality remains usable;
- Guardian/Health/Branch/Working Tree remain readable;
- `Watching this repository` and repository link remain visible;
- toolbar remains below the repository strip;
- no overlap, clipping, or accidental horizontal scrolling occurs.

## Safety boundary

This is a presentation/layout change only. It must not alter Git state, remote operations, repository URLs, project selection, branch state, or saved configuration.