# ADR-0005 — Block release publication on source provenance mismatch

Status: Accepted
Date: 2026-09-11

## Context

`scripts/build-release.ps1` builds a release package (installer, portable executable, manifest, checksums) at a point in time, recording the branch and exact commit it was built from. Between that build and the maintainer actually pressing "publish," it's possible for the source repository to have moved on — a further Save, a Send/Get that changed the branch tip, or simply forgetting the package is now stale.

## Decision

`ApplicationReleaseForm`/`GitHubReleasePublisher` refuse to publish unless the prepared package's recorded source branch and commit exactly match the currently checked-out branch and HEAD commit, in addition to requiring a clean working tree and local/`origin` alignment. On a mismatch, the UI states plainly that publication is blocked and that the package must be rebuilt, rather than offering to publish an out-of-date package or silently trusting its self-reported version.

## Reasons

- A release manifest's `Version` field is self-reported by the build process; without an independent check, publishing could ship a package that no longer matches the source it claims to represent.
- Blocking rather than warning removes the chance of a rushed "publish anyway" click producing a mismatched public release.
- Requiring an exact rebuild after any further change keeps "what was tested" and "what gets published" from silently drifting apart.

## Consequences

- Any Save/Send made after `build-release.ps1` runs invalidates the prepared package for publishing purposes, even if the actual file contents wouldn't have changed — the maintainer must rerun the build script.
- This adds a manual rebuild step to the release process whenever the source moves between packaging and publishing, which is a deliberate cost in exchange for the safety guarantee.
- Combined with the separate check that a release tag isn't overwritten, this means a maintainer can never accidentally publish a release from a commit other than the one they intended, nor overwrite one that's already public.

## Alternatives considered

- **Trust the manifest's self-reported version alone** — rejected: this is exactly the gap that made the check necessary; a manifest can't validate itself.
- **Warn but allow publishing anyway** — rejected: undermines the whole point, since a maintainer under time pressure is exactly the person most likely to click through a warning.
