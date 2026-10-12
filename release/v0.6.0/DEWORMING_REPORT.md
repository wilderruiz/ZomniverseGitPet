# ZGit Pet v0.6.0 — Deworming Report

**Visit date:** 2026-10-12  
**Previous public baseline:** v0.5.0 (`5be903dd331bbbc230c721554e30ed4fb160f349`)  
**Release generation:** 0.6.x  
**Intake delta:** 152 commits were present between the v0.5.0 tag and the first v0.6.0 clinic inventory.  
**Final package provenance:** recorded separately in `release-manifest.json` at package-build time.

> The veterinary names are jokes. The engineering details are the actual treatment record.

## Chief complaint

v0.5.0 gave the animal a strong Guardian command center, project/publishing workflows, release packaging, installed self-update and the live desktop pet. The next problem was deeper: reconciliation still needed a way to **understand divergence before acting on it**, and several surrounding workflows needed stronger isolation, transaction boundaries and Windows-specific diagnostics.

v0.6.0 is therefore a major internal-care visit. The largest organ transplant is the **Reconcile Inspector**, but the visit also treats repository creation, shared-root logical projects, suspicious-path review, Windows path/lock diagnosis, publication workspaces, repository relocation and release-regression reliability.

---

# 1. Vaccination: Three-way Reconcile Inspector

### Condition before treatment

A diverged repository could tell the user that LOCAL and REMOTE had both changed, but understanding *how* they changed was still too coarse. Whole-file choices were available during a conflict, but there was no review-first BASE/LOCAL/REMOTE workspace that could answer:

- what common BASE contained;
- what LOCAL changed relative to BASE;
- what REMOTE changed relative to BASE;
- whether the changed BASE coordinate ranges overlap;
- whether Git can create a clean textual three-way candidate;
- whether an integration-only edit is needed before the final merge is saved.

### Treatment

The Reconcile Inspector was built in phased layers rather than as one opaque merge UI.

#### Phase 1 — review shell

Workboard states such as `LOCAL`, `REMOTE`, `BOTH SIDES` and `CONFLICT` gained a dedicated read-only Inspector entry. Merely opening the Inspector performs no checkout, merge, stage, commit or push.

#### Phase 2 — pinned source identity

GitPet now resolves and pins:

- `BASE` = merge-base;
- `LOCAL` = inspected local commit;
- `REMOTE` = inspected `origin/<branch>` commit, or `MERGE_HEAD` when already reconciling.

Source is loaded from immutable Git objects, including explicit `NOT PRESENT` states for files missing on one side. This separates historical evidence from mutable working-tree content.

#### Phase 3 — two-column source workspace

The Inspector gained:

- BASE ↔ LOCAL;
- LOCAL ↔ REMOTE;
- BASE ↔ REMOTE;
- merged-candidate view;
- linked scrolling;
- syntax-aware source rendering;
- changed-line highlighting based on BASE-derived hunks;
- Max/Restore without replacing the live control;
- persistent splitter ratio;
- `Pretty` and `Exact` presentation modes.

`Pretty` is explicitly display-only. Raw source remains the backing authority for copy, edit and Git operations.

#### Phase 4 — change-shape analysis and merged preview

GitPet now maps zero-context BASE→LOCAL and BASE→REMOTE diff hunks back into BASE coordinates and classifies the relationship. Signals such as `INDEPENDENT CHANGES`, `OVERLAP DETECTED`, `NO OVERLAP DETECTED`, and `CLEAN THREE-WAY MERGE` are deterministic evidence—not a promise that the program's behavior is semantically correct.

The merged preview uses `git merge-file -p` against temporary files and therefore does not mutate repository state.

#### Phase 4A — decision-first Summary

Instead of burying the answer under SHAs and source metadata, the Summary promotes:

1. review status;
2. change relationship;
3. overlap;
4. merged-preview state;
5. plain-language interpretation;
6. then the lower-level provenance.

#### Phase 5 — source export

Copy/export was made a first-class review tool:

- selected exact source;
- changed raw block;
- whole raw file;
- code-only bundle;
- current comparison;
- `Copy everything` Markdown;
- `Copy everything` plain text.

Structured exports include the assessment, pinned identities and raw source while deliberately excluding Pretty-mode decoration from source payloads.

#### Phase 6 — LOCAL correction

LOCAL source can be corrected from the Inspector without rewriting history. The draft is validated against the pinned local identity and current working file. `Write local` changes only the working file. `Commit local` stages only the selected path and creates a new correction commit.

No amend, rebase, force push or hidden reconciliation occurs.

#### Phase 7 — REMOTE correction

REMOTE editing is isolated from the primary worktree. `Prepare remote` creates a temporary worktree/branch based on the pinned REMOTE commit and produces a correction commit there. `Send remote` is a separate action.

Immediately before Send, GitPet reads the live branch tip. If another writer moved REMOTE, Send is blocked. Push remains a normal non-force fast-forward attempt.

#### Phase 8 — editable merged candidate

The generated merge remains immutable evidence on the left while the user may explicitly create an editable candidate on the right. Candidate acceptance revalidates LOCAL, tracking REMOTE, live REMOTE and the regenerated preview, then starts the normal `git merge --no-commit --no-ff` reconciliation, writes/stages the approved path and deliberately stops with `MERGE_HEAD` present.

The existing **Save reconciliation** action remains the commit gate. Candidate acceptance never sends anything.

#### Phase 9 — isolated validation

Proposed source can be checked in a disposable detached Git worktree before mutation.

Deterministic checks include:

- NUL/conflict-marker rejection;
- JSON parsing;
- XML/SVG/XAML/csproj/props/targets parsing;
- CSS-family structural checks;
- `php -l` when PHP is available;
- `node --check` for JavaScript-family source;
- `python -m py_compile` for Python;
- PowerShell parser-only validation;
- project-saved Test Commands run in order in the isolated worktree.

Saved test failures are treated as important evidence, not an infallible oracle: the user can explicitly continue after reviewing a failure. Deterministic source-validation failures remain blocking.

Validation can be cancelled with `Stop check`; the draft survives, and the disposable worktree is cleaned in `finally`.

#### Phase 10 — operational polish

The Inspector gained:

- `Ctrl+1…5` deterministic tab navigation;
- `Ctrl+P` Pretty/Exact;
- `Ctrl+Shift+C` Copy everything;
- `Ctrl+Enter` draft validation;
- `F6` source-pane focus switching;
- `F11` Max/Restore;
- safe `Esc` cancellation;
- accessibility names/descriptions;
- per-tab scroll snapshots;
- large-source Exact Performance Mode that keeps full raw text but skips expensive presentation passes.

### Clinical outcome

Reconciliation is no longer merely a warning or binary whole-file choice. It is a review-first workflow with immutable evidence, explicit correction boundaries, isolated validation and preserved final human approval.

---

# 2. Parasite specimen: ContextMenu self-destruction

**Affected organ:** Reconcile Inspector Copy menu  
**Observed:** 2026-09-30

### Symptoms

Opening the Copy menu, dismissing it and then reopening it could crash with:

`Cannot access a disposed object. Object name: 'System.Windows.Forms.ContextMenuStrip'.`

### Root cause

The menu disposed itself from its own `Closed` lifecycle. The control could still be referenced by the Inspector even though WinForms considered it disposed.

### Treatment

Replace ephemeral self-disposal with an Inspector-owned persistent `ContextMenuStrip`. Reuse it for subsequent openings and dispose it only with the Inspector.

### Evidence

Manual smoke covered open → dismiss → reopen, and copy/export regression coverage was hardened around raw source and Markdown fences.

### Outcome

**Parasite removed.** Copy menu lifetime now matches its owner.

---

# 3. Parasite specimen: hidden LOCAL correction action

**Affected organ:** Inspector action-state projection  
**Observed:** 2026-09-30

### Symptoms

`Edit local` could disappear after source load/reparent/state transitions even when a valid LOCAL snapshot existed.

### Root cause

Visibility was not being explicitly reasserted from the authoritative idle/source state after all UI transitions.

### Treatment

Centralize edit-action refresh and derive visibility/enabled state from:

- not currently editing;
- source model loaded;
- selected source exists;
- correct view/state.

### Outcome

**Parasite removed.** Valid LOCAL snapshots consistently expose the correction action.

---

# 4. Parasite specimen: stale REMOTE correction race

**Affected organ:** REMOTE Send  
**Risk class:** history safety

### Symptoms

A correction can be prepared from REMOTE commit `R1`, then another machine/user advances the real remote branch to `R2` before GitPet sends the prepared correction.

Without a second check, the prepared state is stale.

### Treatment

REMOTE Send now:

1. calls `git ls-remote --heads origin refs/heads/<branch>` immediately before publication;
2. requires the live SHA to equal the pinned REMOTE SHA;
3. verifies the correction descends from the pinned REMOTE commit;
4. uses an ordinary non-force push;
5. verifies the live remote tip after Send;
6. cleans the temporary worktree/branch.

### Regression evidence

The moved-REMOTE regression prepares a correction, advances origin from another clone, attempts stale Send and proves:

- Send is rejected;
- origin retains the other writer's newer commit;
- GitPet's correction is not pushed;
- primary HEAD/file remain unchanged;
- explicit cleanup removes temporary state.

### Outcome

**Parasite contained at two barriers:** explicit live-tip check plus Git's own non-fast-forward push protection.

---

# 5. Parasite specimen: suspicious `token` false positives

**Affected organ:** suspicious-path protection  
**Observed:** innocent paths such as `Styles/tokens.css`

### Symptoms

A broad legacy `token` substring rule could classify ordinary styling/design-token files as secret-bearing paths.

### Root cause

The old pattern treated a common programming/design term as equivalent to credential provenance.

### Treatment

- migrate legacy broad patterns to filename/secret-aware rules;
- surface the exact matching rule during review;
- let manual Save/reconciliation continue only after explicit human approval;
- keep automatic Save conservative and non-interactive;
- add regressions for false positives.

### Outcome

**Parasite removed without lowering secret hygiene.** Real suspicious paths remain protected while common source names stop tripping the old broad rule.

---

# 6. Parasite specimen: validation command quoting damage

**Affected organ:** isolated Reconcile validation

### Symptoms

A generated command-quoting helper was malformed during implementation, creating a compile/runtime risk for language validator invocation.

### Root cause

String escaping across C#/shell boundaries was encoded incorrectly.

### Treatment

Repair the command quoting helper, re-run symbol-level checks, and retain validation regression coverage across deterministic parsers and saved Test Commands.

### Outcome

**Parasite removed.** Optional PHP/Node/Python/PowerShell checks can be launched without corrupting their command line.

---

# 7. Parasite specimen: keyboard escape from locked edit view

**Affected organ:** Inspector navigation

### Symptoms

Disabled tab buttons correctly prevented mouse navigation during an active edit, but keyboard shortcuts could call view-selection code directly and escape the locked LOCAL↔REMOTE or Merged editor view.

### Root cause

The lock existed only in presentation/button state instead of at the authoritative navigation boundary.

### Treatment

Move the edit-view guard into `SelectView` itself. Keyboard, mouse and programmatic selection now pass through the same invariant.

### Outcome

**Parasite removed.** Navigation policy no longer depends on input device.

---

# 8. Vaccination: active merged conflict choice

### Condition before treatment

Once a real Git merge had started, textual conflicts were historically resolved as whole-file LOCAL or ONLINE choices.

### Treatment

Active textual conflicts can now offer **Use merged version**. The merged-resolution editor:

- presents generated vs resolved text side by side;
- rejects unresolved `<<<<<<< / ======= / >>>>>>>` marker blocks;
- writes/stages only the resolved selected path;
- preserves `MERGE_HEAD`;
- leaves final reconciliation commit creation to normal Save.

Standalone project reconciliation intentionally remains Local/Online-only where its isolation model differs.

### Outcome

The animal can now preserve independent work from both histories even after entering active merge state.

---

# 9. Parasite specimen: unrelated-history reconciliation

**Affected organ:** reconcile preflight  
**Observed:** 2026-10-08

### Symptoms

Repositories with unrelated histories could proceed too far toward merge machinery before the incompatibility was explained.

### Root cause

A common ancestor was assumed later in the flow instead of being treated as a precondition.

### Treatment

Detect the absence of a merge-base and block reconciliation before merge.

### Outcome

**Parasite removed.** GitPet refuses to present unrelated lineages as an ordinary divergence problem.

---

# 10. Vaccination: first-class GitHub repository creation

### Previous condition

Repository creation was too entangled with current-project context and connection wizard assumptions.

### Treatment

A dedicated GitHub repository creation flow was added with:

- Projects-menu entry independent of current project;
- owner/account selection;
- repository naming/details;
- privacy choice;
- optional initial README;
- corrected owner typing;
- resizable/polished form behavior;
- connection flow support after creation.

### Outcome

Creating a repository is now its own deliberate operation instead of an incidental branch of another wizard.

---

# 11. Vaccination: sibling logical projects on one repository root

### Previous condition

Logical project identity and repository-root identity could be too tightly coupled, making multiple scoped projects over the same Git history awkward or ambiguous.

### Treatment

Project identity was separated from repository-root identity. Sibling logical projects can now share the same root while preserving their own scope and project-facing context. Regression coverage verifies the shared-root model.

### Outcome

One Git repository can safely host multiple GitPet project views without pretending they are different histories.

---

# 12. Vaccination: review-first publication workspace

### Previous condition

Standalone/public publication needed a clearer review mode and stronger compatibility with isolated publication state.

### Treatment

v0.6.0 added a review-first publication workspace mode and strengthened standalone publication compatibility. Follow-up fixes kept controls interactive and made the output destination explicit.

### Outcome

Publication is easier to inspect before writing external output, with fewer hidden assumptions about where artifacts go.

---

# 13. Parasite specimen: publication workspace frozen controls

**Affected organ:** publication workspace UI

### Symptoms

Controls could become effectively non-interactive during state transitions.

### Treatment

Separate review-state gating from controls that must remain available, stabilize enabled-state projection, and ask explicitly where output should be saved.

### Outcome

**Parasite removed.** Review-first publishing remains navigable and explicit.

---

# 14. Vaccination: cross-drive repository relocation

### Previous condition

Moving a repository is simple within one filesystem but significantly riskier across drives because an atomic rename is unavailable.

### Treatment

A dedicated `CrossDriveRepositoryMoveRuntime` was added. The relocation workflow was then hardened as a transaction and unified with project reassignment so the UI no longer exposes two competing mental models for the same move.

The design emphasizes verification and recovery rather than pretending a cross-volume copy is atomic.

### Outcome

GitPet can safely support repository mobility beyond same-drive rename semantics.

---

# 15. Parasite specimen: Windows path block vs file lock confusion

**Affected organ:** Windows recovery diagnostics  
**Observed:** 2026-09-22

### Symptoms

Windows can deny creation/movement for reasons that are not a conventional open-file lock. Treating every access/path block as a lock can lead to bad advice such as offering to close unrelated processes.

### Treatment

A Windows lock-owner service now distinguishes confirmed Restart Manager ownership from ownerless path-creation failures. GitPet offers process-closing only when an actual owner is identified; otherwise it gives safer restart/save-work guidance.

### Outcome

**Parasite removed.** Recovery advice now matches the evidence available from Windows.

---

# 16. UX treatment: repository visibility and About surfaces

v0.6.0 also improves orientation around the repository itself:

- clickable repository web address in Guardian;
- enough header space for that address;
- correct clearing when repository context is unavailable;
- standalone project repository URL visibility;
- About-window refinement and related project links.

These are not safety-critical treatments, but they reduce context mistakes when one Guardian can manage many repositories/projects.

---

# 17. Release-gate deworming discovered during v0.6.0 packaging

The release clinic itself found parasites in the **tests**, which is exactly why the packaging gate exists.

## 17.1 Self-dirtying Phase 6 regression

### Symptom

The LOCAL edit regression failed with:

`Local correction commit did not produce a clean new HEAD.`

### Root cause

The regression created `audit.jsonl` inside the disposable Git repository. Its own audit telemetry therefore made `git status` dirty after the correction commit.

### Treatment

Move regression audit output outside the test repository and assert the actual LOCAL correction contract: HEAD advances, selected path is committed/clean, staging contains no leftovers, and stale pinned-HEAD validation is rejected.

### Outcome

The test no longer poisons its own fixture.

## 17.2 Platform-sensitive LOCAL fixture

### Symptom

The exact-file regression could inherit host Windows Git line-ending behavior.

### Treatment

Set `core.autocrlf=false` inside the disposable fixture so source semantics do not depend on the developer's global Git settings.

### Outcome

Deterministic test repository.

## 17.3 Merge-abort regression compared console formatting instead of blobs

### Symptom

The Phase 8 regression reported:

`Merge abort did not restore the pinned LOCAL commit / clean selected path.`

while production `git merge --abort` behavior was correct.

### Root cause

The test used ordinary `RunGitAsync(["show", "HEAD:shared.txt"])` and compared that returned text against a source literal containing the final newline. Ordinary GitPet command output goes through `JoinOutput`, which intentionally `TrimEnd()`s stdout for human-readable command messages. The regression was therefore comparing presentation-formatted console output to exact source bytes.

### Treatment

Replace text comparison with Git object identity:

- `git rev-parse HEAD` must equal pinned LOCAL;
- `git rev-parse HEAD:shared.txt` obtains the committed blob identity;
- `git hash-object shared.txt` obtains the restored working-file blob identity;
- those blob SHAs must match;
- selected-path status must be empty;
- cached/staged diff must be empty.

Failure diagnostics now print each invariant independently.

### Outcome

The regression finally tests what it claimed to test: **exact file restoration**, including newline bytes, without depending on console-output trimming.

---

# 18. Regression certificate

Before the v0.6.0 release-record feature was added, the complete release regression executable passed the following major families in one run:

- application identity and installed/portable/DEV handoff;
- updater version and SHA-256 rules;
- concurrent audit writes;
- first-run onboarding;
- GitHub release publisher provenance;
- Guardian workboard projection;
- logical projects and scope allow-lists;
- project switching visual state;
- repository connection helpers;
- Save visual state and preflight parsing;
- 352-file Save preflight batch behavior;
- cancellation;
- standalone publication isolation/branch/content-fingerprint behavior;
- File Review queue isolation;
- Reconcile Inspector source/analysis/preview;
- LOCAL edit;
- REMOTE edit and moved-REMOTE rejection;
- merged candidate acceptance and abort restoration;
- active merged conflict resolution;
- validation sandbox;
- Windows long paths;
- Windows file-lock ownership.

The release packager reruns the regression harness from isolated Release artifacts before it generates installer/portable assets.

---

# 19. Discharge instructions

For v0.6.0 publication:

1. keep the source tree clean;
2. ensure LOCAL HEAD equals `origin/main`;
3. build the release through GitPet's application-release workflow;
4. let the isolated release build rerun the complete regression suite;
5. inspect `release-manifest.json` provenance, hashes and vet-visit summary;
6. publish only through the explicit GitHub release step;
7. never overwrite an existing release tag;
8. preserve this report as immutable treatment history once v0.6.0 is public.

**Final diagnosis:** substantially healthier animal; many more organs, fewer worms.
