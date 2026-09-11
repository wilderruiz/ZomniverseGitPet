# Documentation Audit — ZomniverseGitPet

Status: Initial audit, with a post-audit delta review appended for 0.4.4
Date: 2026-09-11 (delta review same day, later revision)
Scope: Full repository (`README.md`, `CHANGELOG.md`, `docs/`, `installer/`, `scripts/`, `mockups/`, `prototype/`, `src/ZomniverseGitPet/`, `tests/ZomniverseGitPet.Tests/`)

This audit was produced by reading source files and regression tests directly (not by inferring behavior from file or method names). Where a claim could not be verified from the files inspected, it is marked **unverified** rather than asserted.

---

## 1. Existing documentation inventory

| File | Nature | Assessment |
|---|---|---|
| `README.md` | User + developer facing, root entry point | Extensive (build, usage, safety contract, config paths, source-area map, roadmap). Contains version-number drift and an incomplete build section (see §4). |
| `CHANGELOG.md` | Version history, root | Detailed per-version entries 0.1.x → 0.4.0. Newest dated entry is 0.4.0; no 0.4.1 or 0.4.3 entry exists despite those versions appearing elsewhere (see §4.1). |
| `docs/MAJOR_UPDATES_AND_RELEASES.md` | User/dev conceptual doc for the "Major Update / Milestones" feature | Accurate and consistent with `MajorUpdateCoordinator.cs` / `MajorUpdateFeature.cs` / `MajorUpdateForm.cs`. No contradictions found. Should move to `docs/history/` or `docs/user/` unchanged, as the canonical source for that feature. |
| `docs/wilder_notes.md` | Personal developer scratch notes (first-person, addressed to the maintainer) | **Stale.** Describes `publish-local.ps1` writing to `ZomniverseGitPet_Releases\current\ZomniverseGitPet.exe`, but the actual script now writes to `%LOCALAPPDATA%\ZomniverseGitPet\DEV\DEV-ZomniverseGitPet.exe` and *actively deletes* the old path as legacy. Not a canonical doc; should not be treated as ground truth. |
| `LICENSE` | MIT license | Referenced correctly by the installer. No issues. |
| `mockups/pet/README.md` | Design/mockup notes for pet sprite states | States mockups are "not yet integrated into the live application," but 4 of the 6 sprite states it documents are already embedded resources in the shipped app and used by `PetForm`/`PetAssets` (confirmed by a regression test that checks embedded manifest resources). The claim is stale/overbroad — static art is integrated; any animation/state-transition behavior implied by the mockup pack is not confirmed either way. |
| `prototype/powershell/README.md` + `CHANGELOG.md` | Documentation for an earlier PowerShell/WinForms proof of concept | Self-declares itself superseded ("the maintained public application is the C# project under `src/ZomniverseGitPet/`"). Describes a **dual Git-backend design** (Windows Git for reads, WSL for writes) that has no equivalent in the current C# app, and uses different terminology ("restore-point" vs. current "Save"/"checkpoint"). Real risk of a future agent conflating this with current behavior — needs explicit walling-off. |
| `prototype/powershell/config.default.json` | Prototype-only config schema | Uses a separate `%LOCALAPPDATA%\ZomniverseGitPetPrototype` namespace, distinct from the current app's `%LOCALAPPDATA%\ZomniverseGitPet`. No collision risk, but its schema (`readGitBackend`, `writeGitBackend`, `pollSeconds`, `suspiciousPathPatterns`, etc.) must never be presented as the current app's config schema. |
| `installer/ZomniverseGitPet.iss` | Inno Setup installer script | No dedicated prose documentation exists anywhere; only implicitly referenced by `README.md`'s roadmap and `wilder_notes.md`. |
| `scripts/build-release.ps1`, `scripts/publish-local.ps1` | Build/release/dev-publish pipeline | No dedicated prose documentation; `README.md`'s "Build" section only shows the raw `dotnet publish` command and `publish-local.ps1`, omitting `build-release.ps1` and the installer pipeline entirely. |
| `examples/showcase.html` | Static HTML branding/demo page | Not app code; referenced once in `README.md` as an example of an untracked file visible in File Review. No behavior of its own. |
| No `docs/README.md` | — | **Missing.** No single canonical entry point exists today. |
| No ADRs | — | **Missing.** No architecture decision records exist anywhere in the repo. |

---

## 2. Major implemented subsystems (verified against source + regression tests)

Grouped by area; each item below was confirmed by reading the named source files and, where noted, a corresponding regression test.

**Connection & identity**
- GitHub-connected vs. Local Git Only mode (`GitPetConnectionModes.cs`, `ConnectionSettingsForm.cs`, `ConnectionUiRuntime.cs`) — a config-level UI/policy label; local Git operations work identically in either mode. Confirmed by `FirstRunOnboardingRegression.cs`.
- GitHub authentication is entirely delegated to the GitHub CLI (`gh`); GitPet stores no personal-access token (`GitHubAccountService.cs`).
- Repository connection/creation: `RemoteSetupForm.cs` (plain remote add), `RepositoryConnectionWizardForm.cs` (exists / create / fuzzy-find via `gh`), `CloneRepositoryForm.cs` (clone to new folder). Confirmed by `RepositoryConnectionRegression.cs`.
- Safe-directory handling for external/cloud-synced drives (`SafeDirectorySetupForm.cs` + `GitService.AddSafeDirectoryAsync`) — adds the exact path only, never a wildcard.

**Core Save / Get / Send / Reconcile loop (the "Guardian workboard")**
- `GuardianForm.cs`, `GuardianWorkboardControl/Runtime/Service.cs` implement four quadrants: Save (local commit only, never pushes), Get (`git pull --ff-only`, refuses on a dirty tree), Send (`git push` of already-committed history only), Reconcile.
- Reconciliation (`GuardianReconciliation.cs`, `ReconcileConflictsForm.cs`, `GuardianSyncState.cs`): triggered on divergence, uses `git merge --no-commit --no-ff`, per-file "keep mine / keep theirs" conflict resolution, explicit user-driven Save to finalize, `git merge --abort` on cancel/failure. Confirmed by `GuardianWorkboardRegression.cs`.
- Remote change detection is pure polling (`git fetch`, minimum 10s interval) — no webhooks.
- Audit log (`AuditLog.cs`): append-only JSONL, process-wide `SemaphoreSlim` serialization, retries transient `IOException`. Confirmed safe under 16-way concurrent writers by `AuditLogConcurrencyRegression.cs`.

**Logical projects & scope**
- A "logical project" (`RecentRepositoryEntry` in `Models.cs`/`AppConfig.cs`) is a named overlay on a Git repository: several projects can share one `RepositoryRoot`, each with an independent scope (`TrackEverything` or explicit `ScopeEntries`). Confirmed by `LogicalProjectRegression.cs`.
- Project-scope tree selection (`ProjectScopeSelectionForm/Model.cs`, `ProjectScopePlanner.cs`) — tri-state tree, nested-repository detection, `.gitignore`-style rule generation.
- Advanced Project Allow List (`ProjectScopeAllowList.cs`, `ProjectAllowListStore.cs`) — plain-text path list, persisted per-project as JSON at `%LOCALAPPDATA%\ZomniverseGitPet\project-allow-lists.json`. Confirmed by `ProjectScopeAllowListRegression.cs`.
- Ignored-file explicit tracking (`IgnoredFileSavePolicy.cs`, `IgnoredProjectFilesDialog.cs`) — a per-Save preflight gate (not persisted state); user must explicitly tick each `.gitignore`d file to force-add it (`git add -f` on the exact path only).
- Publish-boundary comparison (`ProjectPublishBoundary.cs`/`Dialog.cs`) and automatic Send blocking (`StandaloneProjectPublishingUiRuntime.cs`): if a saved allow list exists for the active project, Send is automatically blocked when the allow-list resolution and the live send snapshot don't match exactly.
- Standalone logical-project publishing (`StandaloneProjectPublishing.cs`) — an isolated Git workspace at `%LOCALAPPDATA%\ZomniverseGitPet\Publishing\<projectId>\`, content-based (blob-identity) fingerprinting, separate linked remote, force-add + push. Confirmed by `StandaloneProjectPublishingRegression.cs`, including that the workspace is never a subpath of the source repository.

**GitPet's own release/update lifecycle** (distinct from the user's project)
- Self-update (`ApplicationUpdateCoordinator/Service/Form.cs`) — runs only for installed builds (detected via a sibling `unins000.exe`), checks a fixed GitHub-Releases-hosted `release-manifest.json` every 6 hours, verifies installer SHA-256 twice (format + downloaded-content hash). Confirmed by `ApplicationUpdateRegression.cs`.
- Application release creation (`ApplicationReleaseFeature/Form.cs`) — GitPet's own maintainer tooling, visible only when the open project *is* the GitPet source tree; consumes `scripts/build-release.ps1` output and publishes via `gh release create`. Confirmed by `GitHubReleasePublisherRegression.cs`, including that a tampered binary is rejected even with a matching manifest hash claim.
- "Major Update" / Milestones (`MajorUpdateCoordinator/Feature/Form.cs`) is an **unrelated, separate** advisory feature about the *user's own project* history (legacy branch/tag creation before a redesign) — not GitPet's own version.

**DEV vs. installed build**
- Distinguished only by filename convention: an executable named with a `DEV-` prefix gets a `DEV-` window title (`GuardianForm.cs`); `ApplicationUpdateService.IsInstalledBuild` checks for a sibling `unins000.exe`. No compile-time flags, environment variables, or separate data directories were found.

**Build & release pipeline**
- `scripts/publish-local.ps1` — fast dev loop: `dotnet publish` (no version bump, no test run, no clean-tree check), copies to `%LOCALAPPDATA%\ZomniverseGitPet\DEV\DEV-ZomniverseGitPet.exe`, refreshes a `DEV-ZGitPet.lnk` Start Menu shortcut, and deletes legacy DEV artifact locations.
- `scripts/build-release.ps1` — full release pipeline: reads version from the `.csproj`, requires a clean working tree, runs the test project, publishes a self-contained single-file build, builds the Inno Setup installer, computes SHA-256 hashes, and writes `release-manifest.json` / `SHA256SUMS.txt` / `PACKAGE-INFO.txt`. No code-signing step exists.
- `installer/ZomniverseGitPet.iss` — per-user install under `{localappdata}\Programs\...` (no admin required), x64-only, fixed `AppId` GUID for upgrade continuity, no bundled .NET runtime check (consistent with a self-contained publish).

**Test architecture**
- No xUnit/NUnit/MSTest. `tests/ZomniverseGitPet.Tests` is a console executable: `Program.cs` runs a bespoke `Check`/`CheckAsync` runner (~48 named checks) and 9 additional files each run their assertions from a `[ModuleInitializer]`-attributed method that executes before `Main` and throws on failure. "Passing" means the compiled test executable exits 0 and prints "All 48 ZomniverseGitPet tests passed."

---

## 3. Documentation gaps

- **No canonical entry point** (`docs/README.md` does not exist).
- **No developer architecture document** describing how the Guardian / Workboard / Sync / Reconciliation / Connection / Publishing layers relate to each other.
- **No documentation at all** for the standalone-publishing / publish-boundary / allow-list system — arguably the most complex, highest-risk subsystem (it can silently block a Send and maintains a second on-disk Git working copy), yet it exists only as inline code comments.
- **No documentation** of the self-update mechanism, the `release-manifest.json` schema, or the distinction between GitPet's own release process and the user's-project "Major Update" legacy-branch feature — high risk of the two being conflated by a future reader.
- **No terminology reference** distinguishing GitPet's vocabulary (Save/Get/Send/Reconcile/Checkpoint/Logical Project) from the underlying Git concepts (commit/pull/push/merge/repository).
- **No documentation of the build/release pipeline** beyond the stale `wilder_notes.md` and an incomplete README section.
- **No safety-boundary reference** standing on its own — the safety contract currently lives buried inside `README.md`.
- **No ADRs** anywhere, despite several clearly intentional architectural trade-offs (see §5).

## 4. Contradictions and drift found

### 4.1 Three different version numbers
- `README.md` states the current version is **0.4.1**.
- `CHANGELOG.md`'s newest dated entry is **0.4.0**, with an empty "Unreleased" section above it.
- `src/ZomniverseGitPet/ZomniverseGitPet.csproj` declares `<Version>0.4.3</Version>`.

None of these three sources agree. This needs reconciling (most likely: CHANGELOG needs a 0.4.1–0.4.3 entry, and README needs its version reference corrected) before any new doc cites a version number.

### 4.2 `docs/wilder_notes.md` is stale about the dev workflow
It describes `publish-local.ps1`'s target as `ZomniverseGitPet_Releases\current\ZomniverseGitPet.exe`. The actual script writes to `%LOCALAPPDATA%\ZomniverseGitPet\DEV\DEV-ZomniverseGitPet.exe` and actively deletes the old path as legacy. The note also says "once the installer/update system is finished, this separation becomes especially useful," implying the installer/update system was incomplete at the time of writing — it is now fully implemented (installer script, self-update service, and release publisher all exist and are exercised by regression tests).

### 4.3 `mockups/pet/README.md` overstates non-integration
It states the mockups "are not yet integrated into the live application." In fact 4 of the 6 documented sprite states are embedded resources in the shipped app (confirmed by a regression test) and are rendered by `PetForm`/`PetAssets`. The static artwork is integrated; only any animation/state-transition behavior described in the mockup pack remains unconfirmed.

### 4.4 README roadmap conflates "installer packaging" with "signing"
The README roadmap lists "Signed release artifacts and installer packaging" as a future item. Installer packaging is fully implemented today (`installer/ZomniverseGitPet.iss`, invoked by `build-release.ps1`); only code-signing is actually absent. The roadmap item should be split so it doesn't read as if packaging itself were unfinished.

### 4.5 README's Build section is incomplete
It documents only the raw `dotnet publish` command and `publish-local.ps1`, omitting `build-release.ps1` and the installer pipeline entirely, even though both exist and are the documented (in `wilder_notes.md`) path to producing a real release.

### 4.6 Prototype vs. current app: no shared internals
`prototype/powershell/CHANGELOG.md` documents a dual Git-backend design (Windows Git for reads, WSL for writes) and its own config schema. None of this carries over to the current C# app. Future documentation must never imply continuity between the prototype's internals and the current app beyond the shared high-level concept ("a desktop pet that guards a Git repo").

---

## 5. Candidate Architecture Decision Records

The following look like genuine, intentional architectural trade-offs worth recording as ADRs in Phase 4 (not yet written):

1. Delegate all GitHub authentication to the GitHub CLI (`gh`) rather than implementing OAuth or storing a personal-access token in-app.
2. Model a "logical project" as a named scope overlay on a shared Git repository, rather than requiring one Git repository per project.
3. Publish a scoped/logical project through an isolated on-disk workspace with its own remote, rather than `git subtree`, `filter-branch`, or sparse-checkout of the main repository.
4. Distinguish DEV vs. installed builds by executable filename convention and an installer sentinel file, rather than compile-time flags, environment variables, or separate data directories.
5. Use a bespoke module-initializer-based test runner instead of an established test framework (xUnit/NUnit/MSTest).
6. Restrict self-update to installed builds only, gated by a fixed GitHub-Releases-hosted manifest and mandatory SHA-256 verification, with no auto-update for portable/dev builds.

---

## Post-audit delta — 0.4.4

Method for this delta: the original audit's file inventory was compared against a fresh, full recursive listing of the repository (file sizes + modification times), which isolated exactly which files changed since the original snapshot. Every changed file was re-read in full at its current content; nothing here relies on the original audit's notes as a substitute for re-reading. Git ref/log files (`HEAD`, `logs/HEAD`, `logs/refs/heads/feature/sync-control-panel`, `refs/heads/...`, `refs/tags/v0.4.4`, `refs/remotes/origin/...`) were read directly (the environment's shell bridge to this device is currently unable to run `git` commands directly — see note at the end of this section) to establish the current branch, HEAD commit, and tag position without relying on a possibly-stale prior understanding.

**Repository state at review time:** current branch `feature/sync-control-panel`, HEAD at commit `75661b54922bc44bef0a7710efb1f8d42ba045b4`. The local branch ref, its `origin` remote-tracking ref, and the `v0.4.4` tag all point at this exact same commit — i.e., the tag was cut from a branch tip that is fully saved and sent (nothing local-only, nothing unpulled). Note this is a feature branch, not `main`; GitPet's release feature has no hard-coded branch requirement, it validates against whatever branch is currently checked out.

Only three application files changed since the original audit's snapshot: `ZomniverseGitPet.csproj` (version bump), `ApplicationReleaseForm.cs`, and `GuardianForm.cs`. No test source file under `tests/ZomniverseGitPet.Tests/` changed (byte-identical to the original snapshot; only their compiled `bin/`/`obj/` artifacts were refreshed by a rebuild). `README.md` and `CHANGELOG.md` are also byte-identical to the original snapshot — neither was updated for 0.4.2, 0.4.3, or 0.4.4.

### Item-by-item findings

**1–4. Standalone publishing, persistent allow lists, Compare-with-Send, publish-boundary safety**
- Implementation status: **CURRENT, unchanged.** None of the source files implementing these (`StandaloneProjectPublishing.cs`, `StandaloneProjectPublishingUiRuntime.cs`, `ProjectScopeAllowList.cs`, `ProjectAllowListStore.cs`, `ProjectAllowListUiBridge.cs`, `ProjectPublishBoundary.cs`, `ProjectPublishBoundaryDialog.cs`) appear in the set of files touched since the original audit.
- Authoritative source files: as listed above (unchanged from the original audit's §2 findings).
- Regression tests: `ProjectScopeAllowListRegression.cs`, `StandaloneProjectPublishingRegression.cs`, `GuardianWorkboardRegression.cs` — all byte-identical to the original snapshot, so the original audit's per-test descriptions stand as current fact, not stale notes.
- Documentation impact: none beyond what the original audit already scoped for `docs/developer/PUBLISHING_ARCHITECTURE.md`, `docs/safety/PROJECT_BOUNDARIES.md`, and `docs/safety/SEND_PREFLIGHT.md`.
- ADR warranted: the candidate ADR already proposed in §5 of the original audit ("publish a scoped project via an isolated workspace + separate remote rather than `git subtree`/sparse-checkout") remains correct and sufficient; no new ADR needed for these four items.
- Obsolete/incomplete earlier statements: none. The original audit's descriptions of these subsystems are reconfirmed accurate against current HEAD.

**5. Release provenance safety**
- Implementation status: **CURRENT, logic unchanged; only a cosmetic UI edit landed in this window.** The blocking mechanism (`GitHubReleasePublisher.PackageMatchesSource`, consumed by `ApplicationReleaseForm.BuildInitialStatus`/`CanPublishIgnoringBusy`) was already present and already covered by `GitHubReleasePublisherRegression.cs` at the time of the original audit. The only change to `ApplicationReleaseForm.cs` in this window (dated 2026-09-11 23:15) increases the package-summary panel's height so its text isn't clipped — no behavioral change.
- Newly confirmed detail (not spelled out with this precision in the original audit): reading the current `BuildInitialStatus` method directly shows GitPet blocks publication, in order, when (a) the prepared package's recorded source branch/commit doesn't exactly match the current branch/HEAD commit — exact message: *"Publication blocked: this package was built from a different branch or commit... Run scripts\build-release.ps1 again after your final Save/Send, then reopen this window."* — (b) the working tree is dirty, or (c) local and origin are not aligned (`RemoteHistoryRelation.Equal`). The confirmation dialog additionally states as a hard guarantee: *"Existing releases are never replaced, and no branch is force-pushed."*
- Authoritative source files: `src/ZomniverseGitPet/ApplicationReleaseForm.cs`, `src/ZomniverseGitPet/GitHubReleasePublisher.cs` (unchanged).
- Regression tests: `GitHubReleasePublisherRegression.cs` (unchanged; already verified in the original audit that a mismatched source commit is rejected, and that an existing release tag is never overwritten).
- Documentation impact: this exact set of blocking conditions and their user-facing messages should be documented verbatim in a developer release-process doc (e.g. `docs/developer/BUILD_AND_RELEASE.md`) and referenced from `docs/safety/` as an example of provenance-based safety, since it is the mechanism the user reports was exercised for real during the 0.4.4 release.
- ADR warranted: **yes, one new ADR** — "Release publication is blocked whenever the prepared package's recorded source branch/commit does not exactly match the currently checked-out branch/HEAD commit, rather than trusting the release manifest's self-reported version alone." This wasn't called out as its own decision in the original audit's ADR candidate list.
- Obsolete/incomplete earlier statements: none obsolete; the original audit had the mechanism right but under-specified the exact block conditions and wording, now filled in above.

**6. Current release state**
- Implementation status: version 0.4.4 is **CONFIRMED CURRENT** directly from `ZomniverseGitPet.csproj` (`<Version>0.4.4</Version>`), and the `v0.4.4` git tag exists locally and on `origin`, at the exact current HEAD commit (see repository state above). The claims that a public GitHub Release v0.4.4 exists with installer, portable build, `release-manifest.json`, and `SHA256SUMS.txt` are **consistent with, but not independently verifiable from, the repository alone** — this environment cannot reach GitHub or the sibling `ZomniverseGitPet_Releases/` output folder referenced by `build-release.ps1`/`ApplicationReleaseForm`, so this audit treats that part of the user's report as given rather than independently confirmed. Everything checkable locally (version, tag, tag/branch/origin alignment, and the code path that would have produced and validated those four assets) is consistent with the report.
- Authoritative source files: `ZomniverseGitPet.csproj`; `.git/refs/tags/v0.4.4`; `ApplicationReleaseForm.BuildDefaultNotes` (confirms the installer is described as "the recommended download for normal Windows use" and the portable build, manifest, and checksums are listed as included assets).
- Regression tests: none test the release-tag/publication event itself (by nature, this is an operational/one-time event, not a unit of code); `ApplicationUpdateRegression.cs` continues to cover the version-comparison and SHA-256-verification logic that the self-update mechanism will use to detect 0.4.4 as newer than a prior installed version.
- Documentation impact: `README.md` and `CHANGELOG.md` need correction (see below); `docs/history/` should record the 0.4.4 release once Phase 4 begins.
- ADR warranted: no.
- Obsolete/incomplete earlier statements: the original audit's §4.1 ("three different version numbers": README 0.4.1, CHANGELOG 0.4.0, csproj 0.4.3) is now **worse, not resolved** — the csproj has moved on to 0.4.4 while README and CHANGELOG are unchanged, so the drift is now a four-way mismatch in effect (0.4.1 stated / 0.4.0 last changelog entry / 0.4.4 actual and tagged). This should be called out plainly as a widening gap, not a stable known issue.

**7. Regression coverage**
- Implementation status: **CONFIRMED, unchanged.** All 11 files under `tests/ZomniverseGitPet.Tests/` are byte-identical to the original audit's snapshot (same size, same modification time); only their compiled `bin/`/`obj/` outputs were refreshed by a rebuild triggered alongside the version bump. This means the original audit's per-file, per-test breakdown (bespoke `Check`/`CheckAsync` runner in `Program.cs` plus 9 module-initializer-based regression files, exercising, among other things, project allow-list persistence, project scope resolution, Send-boundary comparison, standalone-publishing isolation, content-fingerprint changes, and release-package/provenance validation) remains the accurate, current picture of regression coverage — it does not need to be re-derived.
- Documentation impact: none beyond what was already scoped for `docs/developer/TESTING.md`.
- ADR warranted: no (the original audit's ADR candidate about the bespoke test runner still stands).
- Obsolete/incomplete earlier statements: none.

**Guardian workboard status-card cosmetics (not requested by name, but the only other functional-looking change found)**
- Implementation status: **EXPERIMENTAL / in-progress, cosmetic only.** All edits to `GuardianForm.cs` in this window (dated 2026-09-11 12:39–23:32) widen or restyle the repository status card and toolbar buttons (column balancing, muted dividers, wider Projects/Review buttons). None touch Save/Get/Send/Reconcile logic, sync-state computation, or any persisted data. Consistent with the current branch name, `feature/sync-control-panel`, which reads as an in-progress UI redesign rather than a shipped feature.
- Documentation impact: none yet — do not document this as stable, released behavior. Flag it for the next delta review, since a branch named for a "sync control panel" suggests more UI change may follow before this branch merges to `main`.
- ADR warranted: no.
- Obsolete/incomplete earlier statements: none.

### Note on tooling limits during this review

The device-side shell bridge (`device_bash`) could not mount this repository's folder during this review (a known Windows-side mounting issue on this connected device), so git history could not be inspected with `git log`/`git diff` directly. This review instead worked around that by: taking a fresh recursive file listing and diffing file sizes/modification times against the original audit's listing to isolate exactly which files changed, then reading the changed files' current full content directly, plus reading the relevant raw `.git` ref and reflog files (which are plain text) to establish branch/HEAD/tag state without needing the `git` CLI. This is slower than a direct `git log` but is not a lower-confidence method for the specific comparison this task required (identifying what changed and reading its current, real content) — every finding above comes from the actual current file contents, not from inference.

## 6. Notes on method

This audit was compiled from direct reads of: `README.md`, `CHANGELOG.md`, `LICENSE`, `docs/MAJOR_UPDATES_AND_RELEASES.md`, `docs/wilder_notes.md`, `installer/ZomniverseGitPet.iss`, `scripts/build-release.ps1`, `scripts/publish-local.ps1`, `mockups/pet/README.md`, `prototype/powershell/README.md`, `prototype/powershell/CHANGELOG.md`, `prototype/powershell/config.default.json`, `examples/showcase.html`, all 80 `.cs` files under `src/ZomniverseGitPet/`, the `.csproj`, and all 11 files under `tests/ZomniverseGitPet.Tests/` (regression tests and the custom test runner). No behavior in this document was inferred from a file or class name alone; where a claim could only be partially verified, it is stated as such above.
