# ZGit Pet — Release Vet Records Implementation Plan

**Status:** 🟡 IMPLEMENTATION ACTIVE — PLAN CREATED / RECORDS + UPDATE UX NEXT  
**Goal:** make every application release feel like a documented veterinary visit for the evolving ZGit Pet while preserving serious technical provenance underneath the playful presentation.

## Product concept

Each release is a **vet visit**. The public-facing language can be playful — deworming, vaccinations, parasites removed, recovery notes — but the underlying record must remain technically precise enough to audit what changed and why.

## Repository structure

Create a top-level `release/` folder with:

- `release/README.md` — explains the veterinary release-record convention;
- `release/VET_PASSPORT.md` — cumulative vaccination/treatment passport across versions;
- `release/v<version>/DEWORMING_REPORT.md` — deep technical bug/remediation log for that release;
- `release/v<version>/UPDATE_SUMMARY.md` — compact user-facing summary suitable for the updater popup/release notes;
- optional future files such as `REGRESSION_CERTIFICATE.md` or `RECOVERY_NOTES.md` when a release needs them.

## v0.6.0 scope

Use `v0.5.0` as the previous public release baseline and document all substantive work between `v0.5.0` and the v0.6.0 release candidate, including:

- Reconcile Inspector Phases 1–10 and active-conflict merged choice;
- LOCAL / REMOTE / merged-candidate editing and safety gates;
- validation sandbox and saved Test Commands;
- remote-race, stale-state, merge-abort, cancellation, worktree-cleanup, and source-preservation fixes;
- copy/export safety and sensitive-path review;
- keyboard/accessibility/large-file UX hardening;
- release-regression fixes discovered during the v0.6.0 packaging attempt;
- any other bug fixes and release infrastructure changes present since v0.5.0.

## Updater UX

The update-available dialog should show a concise **Vet visit summary** for the offered version before the user chooses to update. It should answer:

1. what major capabilities were added;
2. what important bugs/parasites were removed;
3. what safety/reliability work was done;
4. where the user can read the full technical vet record.

The updater summary must be version-specific and ship with the application/release metadata rather than being hardcoded forever to v0.6.0.

## Safety / maintenance rules

- Do not replace the existing changelog; vet records complement it.
- Keep technical names, failure modes, Git commands, affected components, and regression evidence explicit in `DEWORMING_REPORT.md`.
- Keep `UPDATE_SUMMARY.md` short enough for a desktop dialog.
- Update `VET_PASSPORT.md` with one row/entry per bug class, treatment, or major capability delivered in that version.
- Future releases add a new version folder instead of editing old treatment history except to correct factual errors.
- Updater display must fail gracefully if a future release has no vet-summary payload.

## Implementation sequence

1. inventory current updater/release-manifest plumbing;
2. compare `v0.5.0` to current `main` and classify v0.6.0 changes;
3. create `release/` records and v0.6.0 deworming/passport files;
4. add version-specific updater summary payload to release packaging/application metadata;
5. update the update-available dialog to render the summary;
6. add regression coverage for summary parsing/fallback;
7. update docs/changelog and run build/tests/release smoke.

**Exit gate:** v0.6.0 has a complete technical vet record, the cumulative passport is current, and users offered v0.6.0 see a useful summary of the vet visit before accepting the update.
