# ZGit Pet — Release Vet Records Implementation Plan

**Status:** 🟡 CODE + RECORDS + UPDATE UX COMPLETE / BUILD + REGRESSION + RELEASE-SCREEN SMOKE PENDING  
**Goal:** make every application release feel like a documented veterinary visit for the evolving ZGit Pet while preserving serious technical provenance underneath the playful presentation.

## Product concept

Each release is a **vet visit**. The public-facing language can be playful — deworming, vaccinations, parasites removed, recovery notes — but the underlying record must remain technically precise enough to audit what changed and why.

## Repository structure

The repository now uses a top-level `release/` clinic:

- `release/README.md` — explains the veterinary release-record convention;
- `release/VET_PASSPORT.md` — cumulative vaccination/treatment passport across versions;
- `release/v<version>/DEWORMING_REPORT.md` — deep technical bug/remediation log for that release;
- `release/v<version>/UPDATE_SUMMARY.md` — compact user-facing updater summary;
- `release/v<version>/GITHUB_RELEASE_NOTES.md` — ready-to-publish GitHub Release body, including compatibility text/links for older installed versions.

## v0.6.0 scope

The v0.6.0 clinic inventory used public `v0.5.0` as the previous-release baseline. The initial comparison contained 152 commits and was grouped into treatment episodes rather than mechanically copying commit messages.

The technical chart covers, among other work:

- Reconcile Inspector Phases 1–10 and active-conflict merged choice;
- LOCAL / REMOTE / merged-candidate editing and safety gates;
- validation sandbox and saved Test Commands;
- remote-race, stale-state, merge-abort, cancellation, worktree-cleanup, and source-preservation fixes;
- copy/export safety and sensitive-path review;
- keyboard/accessibility/large-file UX hardening;
- GitHub repository creation and shared-root logical projects;
- review-first publication and standalone-publication hardening;
- cross-drive repository relocation and transaction hardening;
- Windows path-block versus confirmed file-lock diagnostics;
- release-regression defects discovered by the v0.6.0 package gate itself.

## Updater UX

The update-available dialog now presents the release as a clinic visit:

- heading: **GITPET IS BACK FROM THE VET**;
- **Vet visit — what changed** summary;
- normal Install update / Later controls;
- GitHub Release page link;
- full **Vet record** link;
- cumulative **Vet passport** link.

### Version-specific summary source

Starting with v0.6.0, the updater first requests:

`release/v<version>/UPDATE_SUMMARY.md`

from the immutable offered release **tag**, not mutable `main`.

If that file is absent or cannot be fetched, the updater falls back to the GitHub Release body. This preserves compatibility with older releases that predate veterinary records.

For the first veterinary release specifically, installed v0.5.0 clients only know the old GitHub-Release-notes path. Therefore v0.6.0 includes `GITHUB_RELEASE_NOTES.md`; its contents must be used as the GitHub Release body when publishing v0.6.0 so both updater generations receive the same treatment summary.

## Safety / maintenance rules

- Vet records complement rather than replace `CHANGELOG.md`.
- Technical names, failure modes, Git commands, affected components, and regression evidence remain explicit in `DEWORMING_REPORT.md`.
- `UPDATE_SUMMARY.md` stays compact enough for the desktop update dialog.
- `VET_PASSPORT.md` records version/date/condition/treatment/outcome across the animal's life.
- Future releases add a new version folder instead of rewriting old treatment history except to correct factual errors.
- The updater fails gracefully to GitHub Release notes and then a generic message if a future record is missing.
- Vet/deworming/passport links are pinned to the offered release tag.
- Installer download and SHA-256 verification behavior is unchanged by this feature.

## Implemented sequence

1. ✅ inventoried current updater/release plumbing;
2. ✅ compared public `v0.5.0` to current v0.6.0 work and classified the changes;
3. ✅ created `release/` clinic records, v0.6.0 deworming report, passport, updater summary and GitHub release notes;
4. ✅ made the updater resolve the versioned tagged summary with legacy release-note fallback;
5. ✅ updated the update dialog with the veterinary summary and record/passport links;
6. ✅ added regression coverage for summary priority, legacy fallback, generic fallback and release-tag-pinned record URLs;
7. ✅ updated user/developer documentation;
8. 🟡 run final local build/regression and inspect the v0.6.0 publishing screen/release notes before publication.

## Final smoke gate

After pulling the implementation:

```powershell
dotnet build ZomniverseGitPet.sln -c Release
dotnet run --project tests\ZomniverseGitPet.Tests\ZomniverseGitPet.Tests.csproj -c Release
```

Then prepare the release through ZGit Pet itself. In the publishing screen, replace/confirm the editable Release Notes field with `release/v0.6.0/GITHUB_RELEASE_NOTES.md` before the final Publish action. This compatibility step is required for installed v0.5.0 clients.

**Exit gate:** v0.6.0 has a complete technical vet record, the cumulative passport is current, the updater renders a version-specific clinic summary, the full regressions pass, and the GitHub Release body carries the v0.6.0 veterinary notes for older clients.
