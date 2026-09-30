# ZGit Pet — Reconcile Inspector Implementation Plan

**Status:** 🟡 IMPLEMENTATION ACTIVE — PHASES 1–8 COMPLETE / PHASE 9 CODE COMPLETE, BUILD + VALIDATION HARDENING SMOKE PENDING / PHASE 10 NEXT  
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
- ✅ **Phase 4 — Change-Shape Analysis + Merged Preview** — **CORE COMPLETE / REAL SUMMARY + CLEAN MERGED PREVIEW SMOKED.** A real Millenova `BOTH SIDES` file reports `INDEPENDENT CHANGES`, `Overlap: NO`, and `CLEAN THREE-WAY MERGE` while preserving pinned BASE / LOCAL / REMOTE identities and leaving repository state untouched.
- ✅ **Phase 4A — Decision-First Reconcile Summary UX** — **COMPLETE / REAL UI SMOKED.** The real Millenova Inspector now surfaces review status, change relationship, overlap, and merged-preview evidence before secondary source/provenance detail.
- ✅ **Phase 5 — Copy Actions + “Copy Everything” Export** — **COMPLETE / REAL MENU + COPY LIFECYCLE SMOKED.** The Copy menu can be opened/dismissed/reopened safely and exports use raw backing source.
- ✅ **Phase 6 — Edit LOCAL Before Reconciliation** — **COMPLETE / REAL UI SMOKED.** Edit local is visible on valid LOCAL snapshots and the Exact-mode correction workflow is available without rewriting history.
- ✅ **Phase 7 — Edit REMOTE** — **COMPLETE / REAL UI SMOKED.** REMOTE correction preparation, isolated worktree state, explicit Send/Discard flow, and remote-tip safety are exercised.
- ✅ **Phase 8 — Editable MERGED CANDIDATE** — **COMPLETE / REAL UI SMOKED.** Generated-vs-edited candidate review, validation, cancellation, and explicit Accept + reconcile UI are exercised.
- 🟡 **Phase 9 — Validation + Race / Safety Hardening** — **CODE COMPLETE / BUILD + REAL VALIDATION SMOKE PENDING.** Deterministic language checks, isolated saved Test Commands, explicit cancellation, stale/race gates, cleanup, audit, failure recovery, and sensitive-copy warnings are wired.
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
13. **Summary is a decision-support surface, not a text dump.** The highest-value deterministic signals — change relationship, overlap, and merged-preview status — must appear first with strong visual hierarchy, spacing, and status semantics. Commit SHAs and lower-level detail remain available but secondary.
14. **Do not overstate safety.** A clean merge preview and `Overlap: NO` are strong deterministic evidence, but the UI should say things like `REVIEW READY`, `NO OVERLAP DETECTED`, and `CLEAN THREE-WAY MERGE` rather than claiming that reconciliation is guaranteed safe.

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

### ✅ Phase 4 — Change-Shape Analysis + Merged Preview

**Status:** CORE COMPLETE / REAL SUMMARY + MERGED PREVIEW SMOKED

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
- clean-merge regression uses non-adjacent independent edits because Git's textual merge engine may legitimately combine adjacent non-overlapping zero-context hunks into one conflict region; analysis and merge-engine outcome remain separate evidence.

Observed smoke: the real Millenova `tools/validate_community_foundation.php` `BOTH SIDES` case reports **INDEPENDENT CHANGES**, **Overlap: NO**, and **CLEAN THREE-WAY MERGE** with pinned BASE / LOCAL / REMOTE revisions visible. The output is correct, but the smoke exposed that the Summary hierarchy is too flat for the importance of these signals. **Phase 4 core behavior is accepted as complete; Phase 4A captures the new decision-summary UX requirement.**

Implement:

- BASE→LOCAL diff model;
- BASE→REMOTE diff model;
- overlap detection;
- deterministic change-shape labels;
- non-destructive candidate generation;
- BEFORE MERGE ↔ MERGED CANDIDATE view.

**Exit gate:** `BOTH SIDES` can distinguish same-path independent edits from overlapping edits before reconciliation begins.

---

### 🟡 Phase 4A — Decision-First Reconcile Summary UX

**Status:** CODE COMPLETE / BUILD + REAL SUMMARY UI SMOKE PENDING

### Why this exists

The Phase 4 smoke showed that the Summary currently contains the right facts but presents them with nearly equal visual weight. For most reconciliation decisions, users should not need to scan commit SHAs, workboard prose, and hunk details before finding the three signals that matter most:

1. **Change relationship** — e.g. `INDEPENDENT CHANGES` vs. `BOTH MODIFIED SAME AREA`;
2. **Overlap** — especially `NO` vs. `YES`;
3. **Merged preview** — e.g. `CLEAN THREE-WAY MERGE` vs. a conflict / unavailable candidate.

These results are expected to be among the most-used parts of Reconcile Inspector and must read like a deliberate decision-support report, not a raw diagnostic text area.

### Summary information hierarchy

The Summary tab should render information in this order:

#### 1. Reconcile Assessment header

A clearly separated, padded top region with a concise heading such as:

```text
RECONCILE ASSESSMENT
REVIEW READY
```

The secondary status must be derived from deterministic evidence only. Do not claim guaranteed safety.

#### 2. Three primary status cards / badges

The first visible row should prioritize:

| Signal | Example clean result | Example caution result |
| --- | --- | --- |
| **Change relationship** | `INDEPENDENT CHANGES` | `BOTH MODIFIED SAME AREA` |
| **Overlap** | `NO OVERLAP DETECTED` | `OVERLAP DETECTED` |
| **Merged preview** | `CLEAN THREE-WAY MERGE` | `CONFLICT MARKERS PRESENT` |

These must be visually stronger than SHAs, path metadata, hunk counts, or workboard detail.

#### 3. Plain-language interpretation

Immediately below the primary signals, show a short deterministic explanation. Example for the real Millenova smoke:

> Both histories changed this file in different BASE locations. No overlapping edit regions were detected, and Git produced a clean three-way merged preview. Review the candidate before reconciling.

This text must explain the evidence without implying an absolute safety guarantee.

#### 4. LOCAL / REMOTE detail cards

Secondary detail should then show:

```text
LOCAL
MODIFIED EXISTING BLOCK
3 hunks

REMOTE
MODIFIED EXISTING BLOCK
3 hunks
```

Local and Remote should be visually balanced and easy to compare.

#### 5. Technical identity / provenance

Pinned revision identity remains available but visually secondary:

- BASE full SHA + locator;
- LOCAL full SHA + locator;
- REMOTE full SHA + locator;
- selected path;
- workboard state/detail.

The user should be able to inspect provenance without technical metadata dominating the decision signals.

### Visual design contract

The Summary should use real WinForms layout controls rather than one monolithic RichTextBox pretending to be a report.

Required qualities:

- generous outer padding;
- clear vertical spacing between sections;
- strong heading hierarchy;
- status cards/pills or bordered panels;
- clean alignment;
- compact but readable typography;
- responsive behavior in embedded and maximized Inspector modes;
- no horizontal clipping at the user's tested Windows DPI;
- no need to scroll before seeing the three primary reconciliation signals.

Suggested status semantics:

- **positive / clean:** existing healthy/green family;
- **caution / review:** existing reconcile/amber family;
- **conflict / overlap:** existing warning/red family;
- **neutral technical metadata:** muted/faint ink.

Color must supplement, not replace, text labels.

### Example target layout

```text
┌──────────────────────────────────────────────────────────────────────┐
│ RECONCILE ASSESSMENT                                  REVIEW READY   │
│                                                                      │
│ ┌────────────────────┐ ┌──────────────────┐ ┌──────────────────────┐ │
│ │ CHANGE RELATIONSHIP│ │ OVERLAP          │ │ MERGED PREVIEW       │ │
│ │ INDEPENDENT CHANGES│ │ NO OVERLAP       │ │ CLEAN THREE-WAY MERGE│ │
│ └────────────────────┘ └──────────────────┘ └──────────────────────┘ │
│                                                                      │
│ Both histories changed different BASE locations. No overlap was      │
│ detected, and Git produced a clean candidate. Review before merging. │
├─────────────────────────────────┬────────────────────────────────────┤
│ LOCAL                           │ REMOTE                             │
│ MODIFIED EXISTING BLOCK         │ MODIFIED EXISTING BLOCK            │
│ 3 hunks                         │ 3 hunks                            │
├─────────────────────────────────┴────────────────────────────────────┤
│ Technical identity / pinned revisions / path / workboard detail     │
└──────────────────────────────────────────────────────────────────────┘
```

### Markdown relationship

The on-screen Summary should **not** simply render raw Markdown as its primary UI. Use structured native controls for the interactive view.

However, its information model should be intentionally compatible with Phase 5 so **Copy summary as Markdown** can export the same hierarchy cleanly.

### Implementation status

Implemented in this slice:

- added a dedicated native `ReconcileSummaryPanel`; Summary no longer renders its report through the left/right code RichTextBoxes;
- added a deterministic `ReconcileSummaryPresentation` model derived only from existing `ReconcileChangeAnalysis` + `ReconcileMergePreview` evidence;
- added a padded **RECONCILE ASSESSMENT** header with `REVIEW READY` / `REVIEW REQUIRED` status;
- added three top evidence cards for **CHANGE RELATIONSHIP**, **OVERLAP**, and **MERGED PREVIEW**;
- positive / caution / conflict semantic colors reuse existing Guardian theme families while every card retains explicit text;
- added a deterministic plain-language interpretation directly below the evidence cards;
- added balanced LOCAL / REMOTE shape + hunk cards;
- moved full BASE / LOCAL / REMOTE SHAs, locators, path, state, and workboard detail into a visually secondary technical section;
- Summary hides code-only **Scroll** and **Pretty/Exact** actions while keeping Max/Restore + Activity available;
- Summary and code views share the same existing Inspector model; no second analysis engine or Git operation was introduced;
- regression coverage proves the clean independent case becomes `REVIEW READY`, overlap/conflict cases become `REVIEW REQUIRED`, and a textual merge conflict cannot be masked by `Overlap: NO`.

Remaining Phase 4A gate: build/test locally, then smoke the real Millenova independent-change case in embedded and maximized Inspector modes. Confirm the three primary evidence cards are immediately visible, provenance remains available below, and no repository state changes.

### Implementation requirements

- introduce a dedicated Summary view/panel instead of writing all summary content into the current left/right text buffers;
- keep Summary state derived from the existing deterministic `ReconcileChangeAnalysis` and `ReconcileMergePreview` models;
- do not add a second analysis engine just for presentation;
- give `INDEPENDENT CHANGES` / `BOTH MODIFIED SAME AREA`, overlap state, and merged-preview state explicit UI elements;
- create a short deterministic interpretation sentence from those existing states;
- retain LOCAL / REMOTE hunk shape/count detail;
- retain pinned revision provenance in a secondary technical section;
- preserve embedded/maximized state behavior;
- preserve all current no-mutation guarantees;
- prepare the Summary data model for Phase 5 Markdown export without implementing Phase 5 copy actions early.

### Regression / smoke contract

Coverage should prove:

- clean independent case renders the three primary statuses correctly;
- overlap case promotes the overlap warning;
- conflicted merged candidate promotes the candidate warning;
- Summary never labels a conflict/overlap case as `REVIEW READY`;
- pinned revision metadata remains present;
- switching Summary ↔ code tabs does not mutate or lose Inspector state;
- embedded and maximized layouts keep the primary status row visible;
- real Millenova smoke shows the decisive signals without requiring the user to scan secondary detail first.

**Exit gate:** a user can open Summary and understand the reconciliation state — especially **independent vs. overlapping**, **overlap yes/no**, and **clean vs. conflicted candidate** — within a few seconds, while still being able to inspect all technical provenance below.

---

### 🟡 Phase 5 — Copy + Export

**Status:** CODE COMPLETE / COPY-MENU LIFECYCLE FIX APPLIED, REAL CLIPBOARD RE-SMOKE PENDING

Implemented in this slice:

- added a compact **Copy ▾** menu plus dedicated **Copy everything** header action;
- **Copy selected** copies only an Exact-view text selection; Pretty-mode selection is intentionally rejected because virtual display lines are not raw Git source;
- **Copy changed block** derives changed raw lines from the active pane's BASE-derived line map rather than copying highlighted UI text;
- **Copy whole file** copies the active pane's raw backing source;
- **Copy code only** exports raw BASE / LOCAL / REMOTE and available merged candidate as lightweight Markdown code sections without assessment/provenance prose;
- **Copy comparison** exports the currently selected raw left/right comparison;
- **Copy everything — Markdown** produces a self-contained ChatGPT-ready bundle with decision summary, plain-language interpretation, file/state/branch, LOCAL/REMOTE hunk shape/count, pinned revision SHAs/locators, raw BASE/LOCAL/REMOTE source, merged-preview status and raw candidate;
- **Copy everything — plain text** provides the same evidence without Markdown formatting;
- Markdown exports use language-aware fences derived from the source path and automatically lengthen the fence when source itself contains backtick runs;
- every successful clipboard action reminds the user that copied source is raw backing text rather than Pretty-view decoration;
- Summary remains copyable through **Copy everything** even though code-specific copy actions require a code/merged tab;
- clipboard exceptions are surfaced in the Inspector footer instead of crashing the UI;
- **smoke correction:** the first real Copy-menu smoke exposed a WinForms lifecycle bug caused by disposing a transient `ContextMenuStrip` inside its own `Closed` event; the menu is now Inspector-owned, reused across openings, and disposed only with the Inspector;
- regression coverage checks language-aware fence selection, raw changed-line extraction, and full self-contained Markdown export content.

Remaining Phase 5 gate: build/test locally, first verify **Copy ▾ → click away → reopen Copy ▾** no longer crashes, then smoke Copy selected in Exact mode, changed block, whole file, comparison, code-only bundle, Copy everything Markdown, and plain-text export against the real Millenova `BOTH SIDES` case. Paste the Markdown bundle into a text editor/ChatGPT and confirm the report is self-contained and source remains exact.

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

### 🟡 Phase 6 — Edit LOCAL

**Status:** CODE COMPLETE / EDIT-ACTION VISIBILITY FIX APPLIED, REAL LOCAL-EDIT RE-SMOKE PENDING

Implemented in this slice:

- added explicit **Edit local** mode; entering it switches to LOCAL ↔ REMOTE and forces **Exact** source so virtual Pretty lines can never be edited;
- the pinned LOCAL commit SHA and raw committed source are captured as an immutable original edit snapshot;
- the LOCAL source pane becomes editable only while edit mode is active;
- dirty state is explicit: `LOCAL DRAFT MODIFIED · not written · not staged · not committed`;
- comparison tabs, Activity, Pretty/Scroll, and Copy actions are locked/hidden while a draft is active so the editable source cannot silently change context;
- **Validate edit** performs non-mutating path containment, logical-project scope, pinned-HEAD, working-tree freshness, text/NUL, and changed-content checks;
- **Write local** writes only the selected file to the working tree, stages nothing, commits nothing, starts no reconciliation, sends nothing, then returns to the normal workboard so the change can follow ordinary Save later;
- **Commit local** first requires a clean staging index, writes the edited file, runs existing Git identity + large-file safeguards, stages only the exact selected path with `SaveStagePlan`, and creates a new `reconcile correction: <timestamp>` local commit;
- Commit local never amends/rebases/force-pushes and never starts Get, Send, or reconciliation;
- suspicious-path confirmation reuses the existing `SuspiciousPathReview` path before either write or commit;
- after a successful correction commit, Guardian/workboard refresh and Reconcile Inspector reload pin the new LOCAL HEAD and regenerate analysis + merged candidate;
- stale drafts are rejected if HEAD moved after the Inspector snapshot was opened;
- existing staged changes block Commit local so unrelated staged work cannot leak into the exact-path correction commit;
- reconciliation source reads now preserve Git stdout exactly, including trailing newline content, instead of trimming source output before edit/copy use;
- **Cancel edit** discards only the in-Inspector draft; before Write local / Commit local, nothing has touched disk;
- regression coverage verifies newline preservation, write-without-commit behavior, exact local correction commit creation, clean post-commit tree, and stale pinned-HEAD rejection.

Validation note: Phase 6 **Validate edit** is a repository/edit-safety validation. Language syntax/structure checks and configured-project test integration remain the dedicated Phase 9 hardening scope.

Smoke correction: the first real Phase 6 UI smoke showed **Edit local** could remain hidden because source load only re-enabled the control without reasserting its visibility after WinForms state/reparent transitions. The Inspector now treats a valid LOCAL snapshot as an explicit visibility invariant: when not already editing, **Edit local** is visible + enabled; when source is unavailable or edit mode is active, it is hidden.\n\nRemaining Phase 6 gate: build/test locally, open the real Millenova `BOTH SIDES` file, confirm **Edit local** is visible, enter **Edit local**, make a harmless local correction, validate it, test Cancel edit, then repeat and use **Commit local**. Confirm only that file is committed, nothing is sent, and the Inspector reloads with a new LOCAL SHA and regenerated summary/merged candidate. Optionally smoke **Write local** separately and verify it returns to the workboard as an ordinary unsaved local change.

Implement:

- explicit Edit local mode;
- immutable original snapshot;
- dirty state;
- edit safety validation;
- exact-path working-file write;
- optional new local correction commit;
- refresh source identities and candidate.

**Exit gate:** a local last-minute correction can be made and committed from the Inspector without rewriting earlier commits or including unrelated staged work.

---

### 🟡 Phase 7 — Edit REMOTE

**Status:** CODE COMPLETE / BUILD + ISOLATED REMOTE-EDIT SMOKE PENDING

Implemented in this slice:

- added **Edit remote** beside Edit local whenever the pinned REMOTE file exists;
- REMOTE editing locks the Inspector to LOCAL ↔ REMOTE, forces **Exact**, and makes only the right REMOTE pane editable;
- draft edits remain memory-only until **Prepare remote** is explicitly confirmed;
- **Validate edit** checks scope/text/origin/pinned object plus the live online branch tip with `git ls-remote`;
- if live REMOTE no longer equals the inspected REMOTE SHA, preparation stops before any worktree/commit exists;
- **Prepare remote** creates a uniquely named OS-temp worktree + temporary local branch rooted at the pinned REMOTE SHA;
- only the selected file is written/staged and the staged boundary is verified before creating the correction commit;
- primary working tree, primary branch, local HEAD, and local source remain untouched;
- prepared state is explicit: correction commit exists locally in isolation but is **NOT SENT**;
- **Send remote** rechecks the live remote SHA immediately before Send;
- moved REMOTE blocks Send and preserves the prepared correction for discard/review;
- the correction must descend from the pinned remote SHA;
- Send uses a normal `git push origin <correction-sha>:refs/heads/<branch>`; there is no force or force-with-lease path;
- a race after the explicit tip check is still protected by the remote's normal non-fast-forward rejection;
- success verifies the online SHA, refreshes `origin/<branch>`, removes the temp worktree, deletes the temp local branch, and reloads the Inspector;
- **Discard remote** removes only temporary local worktree/branch state and never updates the remote;
- Guardian shutdown best-effort-cleans any outstanding prepared session;
- regression coverage uses a real local bare origin and proves prepare/send/cleanup while primary HEAD/file remain unchanged.

Remaining Phase 7 gate: build/test locally. Safe real smoke: **Edit remote → harmless draft → Validate edit → Cancel**, then **Prepare remote → Discard remote**; neither path changes GitHub. Only test **Send remote** with a correction you genuinely want published, because that action intentionally updates the online branch.

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

### 🟡 Phase 8 — Editable MERGED CANDIDATE

**Status:** CODE COMPLETE / BUILD + REAL MERGED-CANDIDATE SMOKE PENDING

Implemented in this slice:

- **Edit candidate** appears only on the pre-reconcile **Merged** tab when a generated file candidate is available;
- entering candidate mode forces **Exact** source and locks navigation to the Merged workspace;
- the two columns become **GENERATED CANDIDATE** (left, immutable) and **EDITED CANDIDATE** (right, editable), preserving the generated merge as evidence instead of replacing it;
- the draft starts as an exact copy of the generated candidate and may also be accepted unchanged;
- **Validate candidate** verifies repository/path scope, clean working tree, no existing `MERGE_HEAD`, current branch, pinned LOCAL SHA, pinned `origin/<branch>` SHA, live remote SHA, and a freshly regenerated candidate snapshot;
- validation rejects NUL text and unresolved Git conflict-marker triplets;
- **Accept + reconcile** is a separate explicit confirmation and does not commit or send anything;
- automatic saving is suspended through the same reconciliation safety mechanism used by the established Reconcile flow;
- acceptance reruns validation, starts `git merge --no-commit --no-ff origin/<branch>`, verifies `MERGE_HEAD` is the pinned REMOTE commit, writes the approved candidate to only the selected path, and stages that path as resolved;
- if candidate application/staging/verification fails after merge start, GitPet runs `git merge --abort` and restores the pre-reconciliation state;
- successful acceptance deliberately leaves `MERGE_HEAD` present so the existing **Save reconciliation** action remains the only commit gate;
- if other files remain unresolved, they are reported and the workboard remains in reconciliation state until those paths are reviewed;
- nothing in Phase 8 pushes, force-pushes, rewrites LOCAL/REMOTE history, or creates a reconciliation commit;
- the core regression uses a real bare origin + two working histories, verifies candidate acceptance leaves local HEAD and remote SHA unchanged, verifies the edited candidate is staged with the correct `MERGE_HEAD`, then proves `git merge --abort` restores the original LOCAL file.

Remaining Phase 8 gate: build/test locally and smoke the real Millenova Merged workspace. Safe UI smoke before accepting: **Merged → Edit candidate → modify harmless text → Validate candidate → Cancel candidate**. Testing **Accept + reconcile** intentionally starts a real local no-commit reconciliation; it still does not commit or send, and can be cancelled through the normal reconciliation Cancel flow.

Implement:

- generated candidate snapshot;
- explicit Edit candidate mode;
- generated vs. edited comparison;
- validation;
- Accept candidate & reconcile action.

**Exit gate:** integration-only fixes can be made to the final candidate while preserving both original histories.

---

### 🟡 Phase 9 — Validation + Safety Hardening

**Status:** CODE COMPLETE / BUILD + REAL VALIDATION HARDENING SMOKE PENDING

Implemented in this slice:

- added a shared `ReconcileValidationService` used by LOCAL, REMOTE, and editable MERGED candidate validation;
- source validation blocks NUL/binary-like text and unresolved Git conflict-marker blocks before mutation;
- JSON uses deterministic `System.Text.Json` parsing;
- XML-family files (`xml/svg/xaml/csproj/props/targets`) use deterministic XML parsing;
- CSS-family files receive quote/comment-aware brace-structure validation;
- when installed locally, PHP uses `php -l`, JavaScript uses `node --check`, Python uses `python -m py_compile`, and PowerShell uses parser-only ScriptBlock creation;
- unavailable optional language tools are reported as skipped rather than falsely failing otherwise valid source;
- saved project **Test Commands** run in order inside a disposable detached Git worktree containing the proposed source;
- merged-candidate validation reproduces LOCAL + REMOTE in the disposable worktree before writing/staging the candidate, so tests see the proposed integrated file rather than either original side alone;
- logical projects map their saved test working directory into the equivalent subfolder of the validation worktree;
- saved-test failure is prominent evidence but is not silently treated as absolute authority: before Write local, Commit local, Prepare remote, or Accept + reconcile, GitPet shows the failed test evidence and requires an explicit **Continue anyway** decision;
- deterministic syntax/structure or validation-infrastructure failure remains blocking;
- **Stop check** cancels an in-progress isolated validation/test run while preserving the Inspector draft;
- opening another Inspector row, returning to Activity, closing Guardian, or disposing the form cancels outstanding validation;
- validation worktrees are removed with best-effort `git worktree remove --force` + prune in `finally`, including cancellation/failure paths;
- validation start/completion/cancellation and external validator commands are audited;
- existing stale-state rules remain authoritative: LOCAL HEAD/working-file checks, branch checks, generated-candidate regeneration, `origin/<branch>` checks, live `ls-remote` checks, and remote Send recheck all run before their respective mutations;
- existing recovery remains intact: merged-candidate failure after merge start aborts the merge, REMOTE preparation cleans temporary worktrees/branches, and LOCAL validation mutates nothing;
- configured suspicious-path patterns now also guard Inspector source export: selected source, changed blocks, whole-file copy, comparisons, and **Copy everything** require the same explicit sensitive-path review before clipboard export;
- regression coverage verifies invalid JSON blocking, passing saved tests, non-blocking saved-test failure evidence, validation cancellation, and cleanup of cancelled validation worktrees.

Remaining Phase 9 gate: build/test locally and smoke a real Millenova PHP edit/candidate. Expected PHP validation when `php` is installed: **Syntax ✓ PHP** plus the active project's saved Test Commands result. Test **Stop check** during a deliberately long configured test if convenient, and verify the draft survives cancellation.

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
