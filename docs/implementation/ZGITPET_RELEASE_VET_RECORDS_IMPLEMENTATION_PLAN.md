# ZGit Pet — Release Vet Records Implementation Plan

**Status:** 🟡 IMPLEMENTATION COMPLETE — VET RECORDS + UPDATER + PUBLISHER NOTES WIRED / FINAL 0.6.0 REBUILD + PUBLISH SMOKE PENDING  
**Goal:** make every application release feel like a documented veterinary visit for the evolving ZGit Pet while preserving serious technical provenance underneath the playful presentation.

## Product concept

Each release is a **vet visit**. The public-facing language can be playful — deworming, vaccinations, parasites removed, recovery notes — but the underlying record must remain technically precise enough to audit what changed and why.

## Repository structure

Create a top-level `release/` folder with:

- `release/README.md` — explains the veterinary release-record convention;
- `release/VET_PASSPORT.md` — cumulative vaccination/treatment passport across versions;
- `release/v<version>/DEWORMING_REPORT.md` — deep technical bug/remediation log for that release;
- `release/v<version>/UPDATE_SUMMARY.md` — compact user-facing summary suitable for the updater popup/release notes;
- `release/v<version>/GITHUB_RELEASE_NOTES.md` — public release body used by the publishing screen and by older installed clients that still consume GitHub Release notes;
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
- repository creation, logical/shared-root projects, branch switching, standalone publishing, repository movement, Windows lock/path diagnosis, and other substantive work present since v0.5.0.

## Updater UX

The update-available dialog shows a concise **Vet visit summary** for the offered version before the user chooses to update. It answers:

1. what major capabilities were added;
2. what important bugs/parasites were removed;
3. what safety/reliability work was done;
4. where the user can read the full technical vet record.

Starting with the v0.6.0 binary, the updater first requests the versioned `release/v<version>/UPDATE_SUMMARY.md` from the immutable release tag. If that file is unavailable, it falls back to the normal GitHub Release body and finally to a generic safe message. The popup exposes direct **Vet record**, **Vet passport**, and **Release page** actions.

Existing v0.5.0 installations cannot receive the new popup chrome retroactively. For the v0.5.0 → v0.6.0 transition, `release/v0.6.0/GITHUB_RELEASE_NOTES.md` therefore carries the same useful treatment summary through the old updater's existing GitHub Release-notes path.

## Publisher UX

The maintainer publishing window resolves default notes from:

`release/<releaseTag>/GITHUB_RELEASE_NOTES.md`

when the prepared package is ready. If the file is absent, unreadable, or empty, publishing remains usable and falls back to the historical generic Windows-package notes. A deterministic regression covers both the veterinary-notes path and the fallback path without opening a UI window.

## Safety / maintenance rules

- Do not replace the existing changelog; vet records complement it.
- Keep technical names, failure modes, Git commands, affected components, and regression evidence explicit in `DEWORMING_REPORT.md`.
- Keep `UPDATE_SUMMARY.md` short enough for a desktop dialog.
- Update `VET_PASSPORT.md` with one row/entry per bug class, treatment, or major capability delivered in that version.
- Future releases add a new version folder instead of editing old treatment history except to correct factual errors.
- Updater display must fail gracefully if a future release has no vet-summary payload.
- Publishing must never be blocked merely because veterinary notes are absent; the generic release-note fallback remains available.

## Implementation sequence

1. ✅ inventory current updater/release-manifest plumbing;
2. ✅ compare `v0.5.0` to current `main` and classify v0.6.0 changes;
3. ✅ create `release/` records and v0.6.0 deworming/passport files;
4. ✅ add immutable tag-based version-specific updater summary resolution with GitHub Release fallback;
5. ✅ update the update-available dialog to render the summary and expose clinic links;
6. ✅ add regression coverage for summary parsing/fallback;
7. ✅ add version-specific GitHub release notes for the v0.5.0 → v0.6.0 compatibility bridge;
8. ✅ preload those notes automatically in the maintainer publishing window with generic fallback;
9. 🟡 run the final build/regression/release-package smoke from the final commit, then publish v0.6.0.

**Exit gate:** v0.6.0 has a complete technical vet record, the cumulative passport is current, users offered v0.6.0 receive a useful summary through the updater generation they already have, and the publishing screen automatically carries the versioned veterinary release body into GitHub publication.
