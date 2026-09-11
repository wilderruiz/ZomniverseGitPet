# ADR-0006 — Restrict self-update to installed builds, with mandatory hash verification

Status: Accepted
Date: 2026-09-11

## Context

GitPet ships in three forms — an installed build (via the Windows installer), a portable single-file executable, and developer (DEV) builds produced locally. Only one of these has a well-defined "replace myself in place" story: the installer knows how to uninstall/reinstall cleanly, while a portable executable or a DEV build could be anywhere, run in any context, or be mid-debugging.

## Decision

`ApplicationUpdateService.IsInstalledBuild` (detected by the presence of a sibling `unins000.exe`) gates the entire self-update feature: `ApplicationUpdateCoordinator.Start()` is a no-op unless this is true. When it does run, it fetches a fixed, GitHub-Releases-hosted `release-manifest.json`, compares versions, and — critically — verifies the downloaded installer's SHA-256 hash against the manifest's recorded hash before ever executing it, deleting the file and aborting on any mismatch.

## Reasons

- An installed build has a known, well-behaved "replace me" mechanism (the Setup installer); a portable exe or DEV build doesn't, and guessing at one risks corrupting a running or hand-managed executable.
- Hash verification means a corrupted or tampered download can never be launched as an update, regardless of how the manifest was served.
- Fixing the manifest's location to a specific GitHub Release asset URL means there's exactly one place a maintainer needs to keep current, and no separate update-server infrastructure to run or secure.

## Consequences

- Portable and DEV users must update manually by downloading a newer build themselves — this is accepted as reasonable for those distribution forms, whose users are already opting out of the installed, managed experience.
- The update mechanism is entirely dependent on GitHub Releases staying available and the manifest being published correctly with every release (see [ADR-0005](ADR-0005-APPLICATION-RELEASE-PROVENANCE.md) for how a mismatched package is prevented from reaching that point).
- A skipped SHA-256 check would be a serious regression; any future change to the update path must preserve this verification step.

## Alternatives considered

- **Self-update all build types uniformly** — rejected: a portable or DEV executable has no reliable "replace this specific file safely" story, especially while it may still be running.
- **Trust the manifest's `Version` field without hash-verifying the download** — rejected: leaves the update path open to serving a corrupted or substituted binary under a valid-looking manifest.
