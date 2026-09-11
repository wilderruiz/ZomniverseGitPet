# Releases and updates

## Getting GitPet

The GitHub Releases page for this repository is the source of truth for the current version and its downloads — this documentation deliberately never hard-codes a version number, since it drifts the moment a new release ships. Each release publishes:

- a Windows installer (the recommended download for normal use),
- a portable, self-contained executable (no installer, no admin rights needed),
- an automatic-update manifest, and
- SHA-256 checksums for the above.

## Automatic updates

If you installed GitPet with the Windows installer, it checks for a newer version in the background (starting shortly after launch, then every few hours) by fetching the manifest published with the latest GitHub Release. If a newer version is available, GitPet asks before doing anything — it never installs silently. Once you approve:

1. GitPet downloads the new installer.
2. It verifies the download's SHA-256 hash against the one published in the manifest before touching it. A mismatch is treated as a failed download, not installed.
3. It launches the installer and closes itself.

**GitPet does not self-update if you're running the portable executable or a development build.** Only an installed copy checks for and applies updates automatically; a portable copy is updated by downloading a newer one yourself from GitHub Releases.

## Related documents

- [Update System](../developer/UPDATE_SYSTEM.md) — the implementation details, and how this differs from a user project's own "Major Update" milestones.
- [Build and Release](../developer/BUILD_AND_RELEASE.md) — how a GitPet release is built and published, for anyone maintaining GitPet itself.
