# ZGit Pet — Reconcile Inspector Implementation Plan

**Status:** 🟡 IMPLEMENTATION ACTIVE — PHASES 1–3 COMPLETE / PHASE 4 CODE COMPLETE, BUILD + REAL MERGED-PREVIEW SMOKE PENDING / PHASE 5 NEXT  
**Parent workflow:** `docs/user/RECONCILIATION.md`  
**Related architecture:** `docs/developer/UI_ARCHITECTURE.md`  
**Safety contract:** `docs/safety/RECONCILIATION_SAFETY.md`  
**Existing review foundation:** `src/ZomniverseGitPet/FileComparisonPanel.cs`  
**Working scope:** Reconcile workboard inspection, BASE / LOCAL / REMOTE comparison, editable correction workflows, merged preview, validation, and structured copy/export  
**Implementation timing:** Build the inspector as a review-first layer before expanding any automatic reconciliation behavior.

## Compact phase overview

**Legend:** ✅ complete foundation · 🟡 next/in progress · ❌ not started

- ✅ **Phase 0 — Existing File Review Foundation** — current File Review already provides a two-column split, Technical view, scrollable text panes, changed-line highlighting, safe text/binary handling, and Before / Now source loading.
- ✅ **Phase 1 — Reconcile Inspector Shell + Workboard Entry** — **COMPLETE / REAL UI SMOKED.** A real Millenova `BOTH SIDES` row opens the dedicated lower-workspace Reconcile Inspector without starting reconciliation; compact/DPI-safe tab sizing is applied.
- ✅ **Phase 2 — Three-Way Source Identity + BASE / LOCAL / REMOTE Loading** — **COMPLETE / REAL SOURCE UI SMOKED.** A real Millenova `BOTH SIDES` file loads pinned BASE / LOCAL / REMOTE SHAs and real PHP source side by side without changing repository state.
- ✅ **Phase 3 — Two-Column Code Comparison Workspace** — **COMPLETE / REAL UI SMOKED.** Syntax coloring, Pretty/Exact view, BASE-derived change highlighting, linked scrolling, persistent splitter, and maximized review have been exercised on the real Millenova PHP comparison.
- 🟡 **Phase 4 — Change-Shape Analysis + Merged Preview** — **CODE COMPLETE / BUILD + REAL PREVIEW SMOKE PENDING.** GitPet classifies hunk shape and overlap from BASE coordinates and generates a read-only three-way candidate outside the repository working tree.
- ❌ **Phase 5 — Copy Actions + “Copy Everything” Export** — copy selection, changed block, whole file, comparison, code-only bundle, plain text bundle, and ChatGPT-ready Markdown diagnostic bundle.
- ❌ **Phase 6 — Edit LOCAL Before Reconcile** — allow deliberate local corrections in the inspector, validate them, write to the working tree, and optionally create a new local correction commit without rewriting existing history.
- ❌ **Phase 7 — Edit REMOTE Before Reconcile** — edit an isolated worktree based on the inspected remote commit, validate and commit there, then permit only an explicit fast-forward remote correction when the remote has not moved.
- ❌ **Phase 8 — Editable MERGED CANDIDATE** — keep generated merge output read-only by default, allow an explicit editable candidate, validate it, and apply it only as the final reconciliation result after approval.
- ❌ **Phase 9 — Validation + Race / Safety Hardening** — syntax/structure checks, configured project tests, remote-moved detection, immutable original snapshots, cancellation, cleanup, audit events, and no-force-push guarantees.
- ❌ **Phase 10 — UX Polish + Regression Coverage + Documentation Rollout** — keyboard flow, maximized editor polish, accessibility, large-file safeguards, regression tests, documentation updates, and release readiness.

> **Maintenance rule:** update this compact overview and the detailed phase status in the same commit as every Reconcile Inspector implementation update. The checklist must always show what is complete, what is next, and what has not started. Never mark planned behavior as current before it is wired and covered by appropriate regression tests.

---

## 1. Goal

Upgrade ZGit Pet reconciliation from a history-level warning into a **review-first visual reconciliation workspace** where a user can understand exactly what changed locally and remotely before GitPet modifies either side.

The inspector should make `BOTH SIDES` genuinely useful. Instead of only saying that both histories touched the same path, GitPet should let the user see:

- what the common BASE contained;
- what LOCAL changed from BASE;
- what REMOTE changed from BASE;
- whether the changes overlap;
- whether one side added a new independent block while the other edited an existing structure;
- what a clean three-way merged candidate would look like;
- whether syntax / structural validation and configured tests pass;
- and, when necessary, safely correct LOCAL, REMOTE, or the merged candidate before the main reconciliation is accepted.

The core interaction model is **two columns side by side**, never a vertical code stack.

---

## 2. Product rules

These rules are non-negotiable unless this plan is explicitly revised.

1. **Code panes contain real source code only.** Semantic explanations, labels, Git metadata, diff markers, line-status prose, and AI-style commentary must never be inserted into the source buffer.
2. **Semantic analysis lives outside the code buffer.** Put explanations in Summary/status surfaces, not between source lines.
3. **Comparison is always two columns.** LOCAL ↔ REMOTE, BASE ↔ LOCAL, BASE ↔ REMOTE, and BEFORE MERGE ↔ MERGED CANDIDATE all use a vertical splitter with left/right panes.
4. **BASE is immutable.** It represents historical evidence and is never editable.
5. **Viewing never changes Git state.** Opening the inspector, switching tabs, maximizing, copying, or generating a preview performs no write, commit, merge, push, reset, or checkout mutation.
6. **Generated merged output starts read-only.** Editing the candidate requires an explicit action.
7. **LOCAL correction does not rewrite history automatically.** A correction becomes working-tree content and, if the user chooses, a new local commit.
8. **REMOTE editing is isolated.** Never edit `origin/<branch>` “in place.” Use a temporary isolated worktree/branch based on the inspected remote commit.
9. **REMOTE correction never force-pushes.** Before publication, re-fetch and verify that the inspected remote commit is still current. If it moved, block the push and refresh the comparison.
10. **Existing reconciliation safety remains intact.** Reconcile continues to be explicit and reviewable; the inspector does not become an excuse for silent merges.
11. **Maximize changes layout only, not state.** Tabs, edits, selections, candidate source, scroll positions, and validation state survive maximize/restore.
12. **Copy actions copy source, not decoration.** Line numbers, background highlighting, UI labels, and semantic summaries are not part of the copied source unless the user chooses the structured “Copy everything” export.

---

## 3. Entry point from the Guardian workboard

### Current workboard states

The Reconcile quadrant already projects paths as:

- `LOCAL`
- `REMOTE`
- `BOTH SIDES`
- `CONFLICT`

### Planned click behavior

Clicking one of those file rows should:

1. leave repository state untouched;
2. switch the lower Guardian workspace from **Guardian Activity** to **Reconcile Inspector**;
3. immediately show a loading state for the selected path;
4. resolve source identity;
5. populate the two-column comparison once ready.

The current top-level **Reconcile** action remains separate. Inspecting a file must not start reconciliation.

### Default comparison selection

| Workboard state | Default inspector view |
| --- | --- |
| `LOCAL` | BASE ↔ LOCAL |
| `REMOTE` | BASE ↔ REMOTE |
| `BOTH SIDES` | LOCAL ↔ REMOTE |
| `CONFLICT` | LOCAL ↔ REMOTE, with conflict status highlighted |

---

## 4. Reconcile Inspector shell

The lower Guardian workspace should become:

```text
RECONCILE INSPECTOR — <relative-path>
BASE <sha> · LOCAL <branch@sha> · REMOTE <origin/branch@sha>

[Summary] [Base ↔ Local] [Local ↔ Remote] [Base ↔ Remote] [Merged Preview]     [⛶ Maximize]

┌───────────────────────────────────┬───────────────────────────────────┐
│ LEFT SOURCE                       │ RIGHT SOURCE                      │
│ identity / source locator         │ identity / source locator         │
│                                   │                                   │
│ real source code                  │ real source code                  │
│ syntax colored                    │ syntax colored                    │
│ changed hunks softly highlighted  │ changed hunks softly highlighted  │
│                                   │                                   │
│ ↕ vertical scroll                 │ ↕ vertical scroll                 │
│ ↔ horizontal scroll               │ ↔ horizontal scroll               │
├───────────────────────────────────┼───────────────────────────────────┤
│ pane actions                      │ pane actions                      │
└───────────────────────────────────┴───────────────────────────────────┘

change summary · overlap state · syntax state · tests state
```

The current `FileComparisonPanel` vertical `SplitContainer` is the design precedent and should be reused/refactored rather than replaced with an unrelated UI model.

---

## 5. Exact source identity

Each pane must identify the real source being displayed.

### BASE

Show:

- merge-base commit SHA;
- repository-relative path;
- label such as `merge-base:<path>`.

### LOCAL

Show:

- local branch;
- exact local commit SHA being compared;
- repository-relative path;
- current filesystem location only where meaningful and safe for the local UI.

Example UI identity:

```text
LOCAL
main @ <local-sha>
<repo-root>\tools\validate_community_foundation.php
```

Public documentation and exported examples must use placeholders rather than personal machine paths.

### REMOTE

Do **not** invent a filesystem location for a Git object.

Show:

```text
REMOTE
origin/main @ <remote-sha>
origin/main:tools/validate_community_foundation.php
```

The remote source is retrieved from Git history, not represented as a fake local path.

---

## 6. Two-column code renderer

The Reconcile Inspector should share rendering infrastructure with the existing File Review Technical view.

### Required behavior

- Cascadia Mono or the existing code font.
- No word wrapping.
- Vertical scrolling.
- Horizontal scrolling.
- Movable vertical splitter.
- Optional synchronized scrolling, enabled by default.
- A visible link/unlink scroll toggle.
- Changed lines use a soft background tint without replacing syntax colors.
- Corresponding hunks should stay visually aligned where practical.
- Binary / unsafe-to-render / very large files get a safe fallback rather than being forced through the source renderer.

### Syntax coloring

Reuse or consolidate the same syntax-coloring method used by Technical view so the two review systems cannot drift.

Target languages already associated with File Review include:

- JavaScript / TypeScript;
- C#;
- PHP;
- Python;
- SQL;
- PowerShell;
- JSON;
- HTML / XML / SVG;
- CSS-family files.

Syntax color is presentation only. The backing text remains exact source.

---

## 7. Full-screen / maximized editing mode

The inspector must support a **Maximize ⛶** control.

Maximize should:

- enlarge the same inspector state rather than create a second independent editor;
- retain the selected comparison tab;
- retain unsaved edits;
- retain the generated merged candidate;
- retain validation results;
- retain scroll/splitter position where possible;
- keep action buttons visible;
- keep the status/footer visible.

The maximized workspace should be appropriate for editing long files without hiding the actions needed to validate, commit, send, or return to the normal Guardian layout.

---

## 8. Change-shape analysis

`BOTH SIDES` only means the same path changed in both histories. The inspector should provide a more useful classification after BASE / LOCAL / REMOTE are known.

Planned classifications include:

- **INDEPENDENT CHANGES** — both sides changed the file but changed non-overlapping regions;
- **NEW BLOCK — LOCAL** — LOCAL inserted a distinct new block relative to BASE;
- **NEW BLOCK — REMOTE** — REMOTE inserted a distinct new block relative to BASE;
- **MODIFIED EXISTING BLOCK — LOCAL**;
- **MODIFIED EXISTING BLOCK — REMOTE**;
- **BOTH MODIFIED SAME AREA** — overlapping changed ranges;
- **DELETE vs MODIFY**;
- **RENAME / COPY interaction** where Git provides enough identity to explain it;
- **STRUCTURE UNKNOWN** when deterministic classification is not reliable.

This analysis is advisory. It must not silently choose which version wins.

---

## 9. Read-only merged preview

Before the user starts the main reconcile operation, GitPet should be able to calculate a candidate result without modifying the working tree.

### View

Use two columns:

```text
BEFORE MERGE                     MERGED CANDIDATE
selected/source context          generated candidate
```

The merged candidate begins read-only.

### Candidate status

Show:

- clean three-way candidate vs. unresolved overlap;
- changed-line count;
- syntax/structure validation state;
- configured test state;
- whether LOCAL or REMOTE changed since the candidate was generated.

The preview must never be represented as already committed or published.

---

## 10. Copy actions

Every code pane should support:

- **Copy selected**
- **Copy changed block**
- **Copy whole file**

Copying code must use the backing source text, not the decorated RichTextBox representation.

### “Copy everything”

Add a deliberately fun, prominent **Copy everything** action for asking ChatGPT or another coding assistant for advice.

Default export format: **Markdown diagnostic bundle**.

Example structure:

```md
# ZGit Pet Reconcile Review

## File
`tools/validate_community_foundation.php`

## Git state
- Base: `<base-sha>`
- Local: `main @ <local-sha>`
- Remote: `origin/main @ <remote-sha>`

## Analysis
- State: BOTH SIDES
- Overlapping hunks: No
- Local: +18 / -2
- Remote: +4 / -1
- Syntax: valid PHP
- Tests: not run

## Base
```php
<exact base source>
```

## Local
```php
<exact local source>
```

## Remote
```php
<exact remote source>
```

## Merged candidate
```php
<exact candidate source>
```

## Local diff
```diff
<deterministic local diff>
```

## Remote diff
```diff
<deterministic remote diff>
```
```

### Export choices

- **Copy everything — Markdown** (default)
- **Copy everything — plain text**
- **Copy code only**
- **Copy comparison only**

Before copying a source bundle, show a lightweight reminder that repository source is about to enter the system clipboard. This is advisory, not a hard block.

---

## 11. Editing LOCAL before reconciliation

LOCAL may be corrected directly from the inspector.

### Flow

```text
LOCAL read-only
    ↓ Edit local
LOCAL EDIT — UNSAVED
    ↓
Validate
    ↓
Save local correction
    ↓
working tree updated
    ↓ optional explicit local commit
recompute inspector
```

### Rules

- entering edit mode is explicit;
- preserve an immutable `LOCAL ORIGINAL` snapshot;
- make unsaved editor state unmistakable;
- validate before offering the commit action;
- use a new correction commit rather than silently amending/rebasing previous history;
- after commit, recalculate BASE / LOCAL / REMOTE and regenerate the candidate.

---

## 12. Editing REMOTE before reconciliation

REMOTE editing requires an isolated environment.

### Planned implementation

1. Capture the inspected remote SHA.
2. Create a temporary isolated Git worktree/branch based exactly on that SHA.
3. Materialize the target source there.
4. Enter `REMOTE EDIT — UNSAVED`.
5. Validate the edit.
6. Create a correction commit in the isolated worktree.
7. Re-fetch the real remote branch.
8. Verify the remote tip still equals the inspected base SHA.
9. If unchanged, offer an explicit **Commit & Send remote correction** fast-forward push.
10. If changed, block publication and require refresh/reinspection.
11. Clean up the isolated worktree when safely possible.

### Explicit prohibitions

Remote correction must not:

- force-push;
- reset the user's main working tree;
- rewrite existing remote history;
- silently merge a newly moved remote;
- push merely because validation passed.

---

## 13. Editable merged candidate

A generated merged candidate remains read-only until the user selects **Edit candidate**.

### Purpose

This is the preferred location for integration-only fixes that conceptually belong to the combined result rather than to LOCAL or REMOTE independently.

### Flow

```text
GENERATED CANDIDATE — READ ONLY
    ↓ Edit candidate
MERGED EDIT — UNSAVED
    ↓
syntax / structural validation
    ↓
configured project tests
    ↓
Accept candidate & reconcile
```

Preserve both:

- `MERGED GENERATED`
- `MERGED EDIT`

so the user's final integration edits remain reviewable.

Accepting the candidate is a separate deliberate action from saving edits inside the editor.

---

## 14. Validation

Validation is evidence, not an automatic merge decision.

### Layer 1 — deterministic source validation

Where practical:

- PHP syntax;
- JSON parse;
- XML parse;
- language/compiler/linter checks already available locally;
- basic structural sanity appropriate to the file type.

### Layer 2 — configured project tests

Reuse GitPet's existing saved Test Commands.

A candidate can report:

```text
Syntax     ✓
Tests      3 / 3 passed
Overlap    none
Remote     unchanged since inspection
```

### Layer 3 — human review

The user still decides whether to:

- correct LOCAL;
- correct REMOTE;
- edit the merged candidate;
- reconcile;
- cancel.

A syntactically valid result can still be logically wrong.

---

## 15. Safety and concurrency

The inspector must treat source identity as pinned snapshots.

### Detect stale state

Invalidate or refresh the relevant comparison when:

- LOCAL HEAD moves;
- the local working file changes externally;
- REMOTE tip moves;
- the active project changes;
- the branch changes;
- a reconciliation starts elsewhere;
- the user edits/commits one side from inside the inspector.

### Remote race rule

A remote edit can only be sent when:

```text
current origin/<branch> SHA == inspected remote SHA
```

Otherwise:

```text
REMOTE CHANGED SINCE THIS EDITOR WAS OPENED

Inspected: <old-sha>
Current:   <new-sha>

Nothing was pushed.
Refresh the comparison before deciding how to proceed.
```

### No destructive convenience

Do not introduce:

- automatic force push;
- automatic reset;
- automatic clean;
- hidden stash;
- automatic history rewriting.

---

## 16. Phase details

### ✅ Phase 0 — Existing File Review Foundation

**Status:** COMPLETE FOUNDATION / EXISTING CODE

Existing capabilities to reuse:

- `FileComparisonPanel`;
- two-column vertical split;
- Human / Technical review concept;
- scrollable source surfaces;
- changed-line background highlighting;
- safe binary / large-file handling;
- exact source retrieval helpers;
- File Review regression coverage.

**Exit condition:** already satisfied. Phase 1 may build on this foundation.

---

### ✅ Phase 1 — Reconcile Inspector Shell + Workboard Entry

**Status:** COMPLETE / REAL UI SMOKED

Implemented in this slice:

- reconciliation rows retain their underlying `GuardianWorkboardRow` model for safe click routing;
- only `LOCAL`, `REMOTE`, `BOTH SIDES`, and `CONFLICT` rows activate the inspector;
- the lower Guardian workspace now has a third exclusive mode: **Reconcile Inspector**;
- the selected path/state is shown immediately;
- the default tab follows the planned row-state mapping;
- the Phase 1 shell already preserves the permanent left/right comparison geometry;
- Activity return is wired;
- source areas explicitly state that source loading begins in Phase 2;
- opening the inspector runs no merge, checkout, commit, reset, or push operation;
- regression coverage locks the accepted row states and default-view routing.

Observed smoke: the Release solution build completed with **0 warnings / 0 errors**, the published DEV app opened a real Millenova `BOTH SIDES` row in the correct LOCAL ↔ REMOTE inspector view, and no reconciliation operation was started. The stale `LogicalProjectRegression` setup exposed by the first full-suite attempt was corrected separately. **Phase 1 is accepted as complete.**

Implement:

- row activation from Reconcile workboard;
- new lower-workspace mode;
- selected path identity;
- loading/error states;
- Activity return action;
- Summary tab placeholder;
- comparison tab strip;
- no Git mutations.

**Smoke:**

- click `LOCAL`;
- click `REMOTE`;
- click `BOTH SIDES`;
- click `CONFLICT`;
- verify the inspector opens without invoking reconciliation;
- return to Guardian Activity.

**Exit gate:** selecting reconciliation rows reliably opens the inspector and never changes repository state.

---

### ✅ Phase 2 — Three-Way Source Identity + Loading

**Status:** COMPLETE / REAL SOURCE UI SMOKED

Implemented in this slice:

- pin current branch, `HEAD`, remote revision, and merge-base using read-only Git commands;
- use `MERGE_HEAD` as REMOTE during an active reconciliation, otherwise `origin/<branch>`;
- load BASE / LOCAL / REMOTE source from immutable commit SHAs;
- keep SHA/ref/path identity in pane headers and real source only in code buffers;
- represent a file absent at one revision with an empty code buffer plus `NOT PRESENT` in its header;
- cancel source loading when another review replaces it, Activity is restored, or Guardian closes;
- keep source panes read-only, no-wrap, and vertically/horizontally scrollable;
- keep Summary metadata separate from source;
- leave Merged intentionally ungenerated until Phase 4;
- regression-test a genuinely diverged temporary repository and prove source inspection does not move HEAD, change the remote-tracking ref, or dirty the working tree.

Observed smoke: a real Millenova `BOTH SIDES` file displayed pinned LOCAL and REMOTE SHAs with the actual PHP source in both panes and a pinned BASE SHA in the footer. **Phase 2 is accepted as complete.**

Implement:

- merge-base resolution;
- pinned LOCAL SHA;
- pinned REMOTE SHA;
- BASE blob loading;
- LOCAL blob loading;
- REMOTE blob loading;
- safe missing/deleted-file representation;
- exact pane identities.

**Exit gate:** BASE / LOCAL / REMOTE can be reproduced deterministically from pinned Git identities.

---

### ✅ Phase 3 — Two-Column Code Workspace

**Status:** COMPLETE / REAL UI SMOKED

Implemented in this slice:

- extracted a shared source renderer used by File Review Technical view and Reconcile Inspector;
- added language-aware token coloring for PHP, C#, JS/TS, Java, Python, JSON, CSS-family, HTML/XML/SVG, SQL, and PowerShell;
- source text remains the real backing code; syntax, change colors, and visual layout are presentation-only;
- supported brace/array-based languages can render a display-only Pretty layout that indents normal structures and expands long one-line arrays/objects into virtual display lines without rewriting pinned Git text;
- every Pretty display line carries provenance back to its original Git source line so changed-line highlighting remains tied to the real diff;
- Reconcile Inspector defaults **Pretty ✓** on and exposes **Exact** for untouched source whitespace;
- BASE ↔ LOCAL and BASE ↔ REMOTE highlight exact changed lines;
- LOCAL ↔ REMOTE highlights each side's changes relative to BASE;
- linked vertical/horizontal scrolling is on by default and can be toggled off;
- the splitter preserves the user's left/right ratio through resize and DPI transitions;
- pane headers stay docked above independently scrollable source panes;
- Max/Restore moves the same live inspector into a maximized resizable window instead of cloning state;
- maximized review retains selected tab, pinned sources, splitter ratio, Pretty/Exact state, and linked-scroll state;
- regression coverage checks BASE→LOCAL / BASE→REMOTE line maps, syntax roles, Pretty provenance, and virtual-line expansion.

Observed smoke: the real Millenova PHP comparison passed the maximized two-column, syntax-color, Pretty/Exact, linked-scroll, changed-line and source-readability checks. **Phase 3 is accepted as complete.**

Implement:

- shared syntax renderer;
- display-only Pretty layout with virtual-line provenance / Exact-whitespace toggle;
- left/right source panes;
- diff-hunk background highlighting;
- synchronized scroll toggle;
- independent scroll mode;
- horizontal scroll;
- resizable vertical splitter;
- maximize/restore;
- sticky pane/action headers.

**Exit gate:** long source files are comfortably reviewable side by side in normal and maximized layouts.

---

### 🟡 Phase 4 — Change-Shape Analysis + Merged Preview

**Status:** CODE COMPLETE / BUILD + REAL PREVIEW SMOKE PENDING

Implemented in this slice:

- parse zero-context diff headers into deterministic BASE/NEW hunk ranges;
- classify each side as NEW BLOCK, MODIFIED EXISTING BLOCK, DELETE, MIXED STRUCTURAL EDIT, or UNCHANGED;
- detect overlap in BASE coordinates, including same-anchor pure additions that line-set intersection would miss;
- summarize same-path divergence as INDEPENDENT CHANGES, BOTH MODIFIED SAME AREA, DELETE vs MODIFY, DELETE vs UNCHANGED, SAME RESULT, or one-sided change;
- generate the three-way candidate with `git merge-file -p` against temporary BASE / LOCAL / REMOTE files under the OS temp directory;
- candidate generation never checks out, stages, merges, resets, commits, pushes, or writes into the repository working tree/index;
- clean merges display the merged source directly;
- textual conflicts remain inspectable with explicit `LOCAL / BASE / REMOTE` conflict markers;
- delete/modify conflicts are reported without inventing a fake candidate;
- Summary now displays the deterministic change-shape analysis and candidate status;
- Merged view displays LOCAL-before-merge ↔ MERGED CANDIDATE side by side;
- existing Pretty/Exact presentation applies to the generated candidate without changing the candidate backing text;
- regression coverage proves independent-hunk classification, same-anchor insertion overlap, clean three-way candidate generation, and unchanged HEAD/remote/working-tree state.

Remaining Phase 4 gate: compile/test locally, then use the real Millenova `BOTH SIDES` file to confirm the Summary label and Merged candidate are sensible and that the working tree remains untouched.

Implement:

- BASE→LOCAL diff model;
- BASE→REMOTE diff model;
- overlap detection;
- deterministic change-shape labels;
- non-destructive candidate generation;
- BEFORE MERGE ↔ MERGED CANDIDATE view.

**Exit gate:** `BOTH SIDES` can distinguish same-path independent edits from overlapping edits before reconciliation begins.

---

### ❌ Phase 5 — Copy + Export

**Status:** NOT STARTED

Implement:

- Copy selected;
- Copy changed block;
- Copy whole file;
- Copy code only;
- Copy comparison;
- **Copy everything — Markdown**;
- plain-text variant;
- language-aware Markdown fences;
- clipboard source reminder.

**Exit gate:** a user can paste a self-contained reconciliation bundle into ChatGPT without manually assembling Git metadata and source.

---

### ❌ Phase 6 — Edit LOCAL

**Status:** NOT STARTED

Implement:

- explicit Edit local mode;
- immutable original snapshot;
- dirty state;
- validate;
- write local working file;
- optional new local correction commit;
- refresh source identities and candidate.

**Exit gate:** a local last-minute correction can be made and committed from the inspector without rewriting earlier commits.

---

### ❌ Phase 7 — Edit REMOTE

**Status:** NOT STARTED

Implement:

- isolated temporary worktree;
- remote-edit branch;
- edit/dirty state;
- validation;
- correction commit;
- remote-tip recheck;
- explicit fast-forward send;
- moved-remote block;
- worktree cleanup.

**Exit gate:** a remote last-minute correction can be safely created and sent without mutating the user's primary working tree or force-pushing.

---

### ❌ Phase 8 — Editable MERGED CANDIDATE

**Status:** NOT STARTED

Implement:

- generated candidate snapshot;
- explicit Edit candidate mode;
- generated vs. edited comparison;
- validation;
- Accept candidate & reconcile action.

**Exit gate:** integration-only fixes can be made to the final candidate while preserving both original histories.

---

### ❌ Phase 9 — Validation + Safety Hardening

**Status:** NOT STARTED

Implement:

- language-aware deterministic checks;
- saved Test Commands integration;
- stale-source detection;
- remote race protection;
- cancellation;
- temporary-worktree cleanup;
- audit events;
- failure recovery;
- sensitive-path handling where source export/edit interactions warrant it.

**Exit gate:** validation and state races fail safely without losing user work or publishing unintended history.

---

### ❌ Phase 10 — UX Polish + Regression + Documentation

**Status:** NOT STARTED

Implement:

- keyboard shortcuts;
- focus order;
- accessibility labels;
- splitter/scroll persistence;
- large-file performance;
- copy-export regression tests;
- local/remote edit regression tests;
- moved-remote regression;
- candidate integrity tests;
- full documentation pass.

Only after implementation is verified:

- update `docs/user/RECONCILIATION.md`;
- update `docs/developer/UI_ARCHITECTURE.md`;
- update `docs/safety/RECONCILIATION_SAFETY.md`;
- update `docs/reference/COMMANDS.md` for any new Git commands;
- update `CHANGELOG.md`;
- mark phases complete here.

**Exit gate:** implementation, regression tests, safety docs, user docs, and this plan all agree.

---

## 17. Suggested implementation components

Names are provisional; source/test evidence wins if implementation evolves.

Potential components:

- `ReconcileInspectorPanel`
- `ReconcileInspectorModel`
- `ReconcileSourceSnapshot`
- `ReconcileComparisonService`
- `ReconcileCandidateService`
- `SharedCodeReviewPane` / extracted renderer from `FileReviewPane`
- `ReconcileCopyExport`
- `ReconcileLocalEditCoordinator`
- `ReconcileRemoteEditCoordinator`
- `ReconcileValidationService`

Prefer extraction/reuse over duplicating File Review rendering logic.

---

## 18. Regression test contract

At minimum, coverage should prove:

- `BOTH SIDES` click opens inspector without reconciliation;
- BASE / LOCAL / REMOTE pin the expected commits;
- independent same-file edits are not labeled as overlapping;
- overlapping hunks are detected;
- source panes contain exact code only;
- copied code excludes line numbers / UI decoration;
- Markdown “Copy everything” contains the expected source identities and fences;
- maximize/restore does not lose dirty editor state;
- LOCAL correction creates expected working-tree content;
- LOCAL correction commit does not amend previous history;
- REMOTE correction uses isolation;
- REMOTE send blocks if the remote tip moved;
- REMOTE correction never force-pushes;
- generated candidate does not modify working files;
- candidate edit remains separate until accepted;
- configured tests are executed only when explicitly requested by the workflow;
- cancellation and failure leave the main repository recoverable.

---

## 19. Out of scope for the first implementation

Do not expand Phase 1 into unrelated functionality.

Initially out of scope:

- AI automatically deciding which side is correct;
- automatic semantic code rewriting;
- force-push workflows;
- arbitrary full IDE replacement;
- package/dependency installation to satisfy validators automatically;
- silent auto-commit;
- silent auto-Send;
- cloud-hosted code analysis;
- replacing Git's merge model.

The first goal is **better evidence and safer human decisions**, not autonomous reconciliation.

---

## 20. Completion definition

This implementation plan is complete when a user can:

1. see `BOTH SIDES` in the Guardian workboard;
2. click the file;
3. inspect BASE / LOCAL / REMOTE as exact syntax-colored source;
4. compare two columns side by side;
5. maximize the inspector;
6. understand whether edits overlap or are independent;
7. preview a merged candidate without changing the repository;
8. copy source or a complete Markdown reconciliation bundle;
9. safely correct LOCAL, REMOTE, or the candidate when necessary;
10. validate the result;
11. see whether the remote changed underneath them;
12. explicitly accept the final reconciliation;
13. retain a clear Git history showing what was corrected and when.

The design succeeds if ZGit Pet helps the user answer **“what changed, where, and what will happen if I reconcile?”** before any irreversible or externally visible action occurs.
