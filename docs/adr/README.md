# Architecture decision records

ADRs capture *why* a decision was made, not how to use the resulting feature — see the linked developer documentation for mechanics. Once accepted, an ADR is never rewritten to match a later implementation change; a later change gets its own, superseding ADR instead.

| ADR | Decision |
| --- | --- |
| [ADR-0001](ADR-0001-GITHUB-CLI-AUTHENTICATION.md) | Delegate all GitHub authentication to the GitHub CLI instead of implementing OAuth or storing a token. |
| [ADR-0002](ADR-0002-LOGICAL-PROJECTS-AS-SCOPED-OVERLAYS.md) | Model a logical project as a named scope overlay on a shared repository, not one repository per project. |
| [ADR-0003](ADR-0003-STANDALONE-LOGICAL-PROJECT-PUBLISHING.md) | Publish a scoped logical project via an isolated workspace and separate remote, not `git subtree`/sparse-checkout. |
| [ADR-0004](ADR-0004-PROJECT-ALLOW-LIST-PUBLISH-BOUNDARY.md) | Treat a saved Advanced Project Allow List as a publishing contract, checked before every scoped Send. |
| [ADR-0005](ADR-0005-APPLICATION-RELEASE-PROVENANCE.md) | Block GitPet's own release publication on exact source branch/commit provenance mismatch. |
| [ADR-0006](ADR-0006-INSTALLED-ONLY-SELF-UPDATE.md) | Restrict self-update to installed builds only, with mandatory manifest + SHA-256 verification. |
| [ADR-0007](ADR-0007-DEV-VS-INSTALLED-BUILD-DETECTION.md) | Distinguish DEV/installed/portable builds by filename convention and a sentinel file, not compile-time flags. |
