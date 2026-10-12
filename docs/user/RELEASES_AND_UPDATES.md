# Releases and updates

## Getting GitPet

The GitHub Releases page for this repository is the source of truth for the current version and its downloads — this documentation deliberately never hard-codes a version number, since it drifts the moment a new release ships. Each release publishes:

- a Windows installer (the recommended download for normal use),
- a portable, self-contained executable (no installer, no admin rights needed),
- an automatic-update manifest, and
- SHA-256 checksums for the above.

Starting with v0.6.0, releases also have a repository-side **vet visit record** under `release/v<version>/`: a short update summary plus a deep technical deworming report. The cumulative [`release/VET_PASSPORT.md`](../../release/VET_PASSPORT.md) records the animal's longer-term evolution across versions.

## Automatic updates

If you installed GitPet with the Windows installer, it checks for a newer version in the background (starting shortly after launch, then every few hours) by fetching the manifest published with the latest GitHub Release. If a newer version is available, GitPet asks before doing anything — it never installs silently.

For charted releases, the update window now opens with a **Vet visit — what changed** summary. That summary is loaded from the offered version's tagged `release/v<version>/UPDATE_SUMMARY.md`, so the text belongs to the immutable release being offered rather than to whatever happens to be on `main` later. The window also links to:

- the full **Vet record / Deworming Report**, containing root causes, treatments and regression evidence;
- the cumulative **Vet Passport**, containing the release/vaccination/bug-treatment history;
- the normal GitHub Release page.

Older releases without a veterinary record remain compatible: GitPet falls back to the normal GitHub Release notes.

Once you approve an update:

1. GitPet downloads the new installer.
2. It verifies the download's SHA-256 hash against the one published in the manifest before touching it. A mismatch is treated as a failed download, not installed.
3. It launches the installer and closes itself.

**GitPet does not self-update if you're running the portable executable or a development build.** Only an installed copy checks for and applies updates automatically; a portable copy is updated by downloading a newer one yourself from GitHub Releases.

## Why the vet theme?

Because this project is literally a pet that keeps evolving. A normal changelog remains the concise software history, while the veterinary record gives complicated releases somewhere to document the ugly parasites too: race conditions, stale-state bugs, platform-sensitive tests, worktree cleanup failures, UI lifecycle crashes and the exact treatment used to kill them.

The terminology is playful. The technical record is intended to be audit-quality.

## Related documents

- [Release Clinic](../../release/README.md) — how vet records are organized.
- [Veterinary Passport](../../release/VET_PASSPORT.md) — cumulative evolution/treatment record.
- [Update System](../developer/UPDATE_SYSTEM.md) — implementation details and compatibility/fallback behavior.
- [Build and Release](../developer/BUILD_AND_RELEASE.md) — how a GitPet release is built and published, for anyone maintaining GitPet itself.
