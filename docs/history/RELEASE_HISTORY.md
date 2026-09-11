# Release history

This is a navigational summary, not a replacement for the root [`CHANGELOG.md`](../../CHANGELOG.md), which remains the detailed, entry-by-entry record. This page exists to record the version-numbering gap discovered during documentation review, so future maintainers don't have to rediscover it.

## Summary by version

| Version | Date | Headline |
| --- | --- | --- |
| 0.1.x | — | Initial PowerShell proof of concept (see [`prototype/powershell/`](../../prototype/powershell/README.md) — historical, superseded). |
| 0.2.0 | — | Rebuilt as a native .NET 8 WinForms application. |
| 0.3.0 | — | Guardian Console visual system introduced; first Push action; recent-project registry. |
| 0.3.1 | — | Connect Remote flow. |
| 0.3.2 | — | Manual Pull button. |
| 0.3.3 | — | Per-project test profiles. |
| 0.3.4 | 2026-09-09 | BEFORE/NOW File Review with syntax highlighting. |
| 0.3.5 | 2026-09-09 | Project scope tree (selective tracking). |
| 0.3.6 | 2026-09-09 | Two-row `.gitignore` builder with preset categories. |
| 0.3.7 | 2026-09-09 | "Trust this project folder?" flow for dubious-ownership recovery. |
| 0.3.8 | 2026-09-09 | Window-placement persistence. |
| 0.4.0 | 2026-09-09 | Major Update / Milestones detector, legacy branch+tag planning, GitHub Release helpers. |
| **0.4.1 – 0.4.4** | — | **No `CHANGELOG.md` entries exist for these versions.** |

## Known gap: 0.4.1 through 0.4.4

`CHANGELOG.md`'s newest dated entry is 0.4.0, but the application has since shipped at least as far as 0.4.4 (confirmed directly from `src/ZomniverseGitPet/ZomniverseGitPet.csproj`'s `<Version>` and a matching `v0.4.4` Git tag). No changelog entries were ever written for 0.4.1, 0.4.2, 0.4.3, or 0.4.4. Based solely on what regression tests and source changes are visible in the repository around this period, the standalone-publishing, allow-list/publish-boundary, and release-provenance systems described throughout this documentation tree were already present at least as of 0.4.3–0.4.4 — but this page does not attempt to reconstruct a version-by-version account of exactly which of those features shipped in which of the missing versions, since no record exists to verify that against. The current, authoritative feature set for whatever version is latest is what this documentation tree describes; GitHub Releases is the source of truth for which version that is.

Closing this gap (writing retroactive or going-forward changelog entries for 0.4.1+) is a `CHANGELOG.md` maintenance task, not a `docs/` one — see [`DOCUMENTATION_MAINTAINER.md`](../DOCUMENTATION_MAINTAINER.md) for the standing rule that a version bump should come with a changelog entry.

## Related

- [Major Updates and Releases](MAJOR_UPDATES_AND_RELEASES.md) — the *feature* for a user's own project history, not GitPet's own release history.
- [Build and Release](../developer/BUILD_AND_RELEASE.md) — how a GitPet release is actually built and published.
