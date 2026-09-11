# Documentation Maintainer Contract — ZomniverseGitPet

This file is the standing contract for anyone — human or AI (Claude, Codex, or otherwise) — doing documentation work in this repository. It applies to every future documentation change, not only the initial Phase 4 pass that created this tree.

## Core principle

Documentation must describe the system that **exists**, not the system anyone wishes existed. A doc's job is to be a faithful map of current, checked-in behavior — not a proposal, not a memory of intent, not an inference from a class or file name.

## Before touching any documentation

1. **Inspect the implementation before documenting it.** Read the actual source files that back a claim. Never write a claim about behavior based only on a file name, a class name, or a comment's stated intent — comments and names drift from code.
2. **Inspect the relevant regression tests.** If a behavior is covered by a test under `tests/ZomniverseGitPet.Tests/`, read the test's assertions, not just its name — names can be misleading, and the arrange/act/assert is the real specification.
3. **Treat source code and tests as authoritative.** If a doc and the code disagree, the doc is wrong — fix the doc. If a doc and a test disagree about a behavior the test actually exercises, the test wins.
4. **Never silently turn PLANNED behavior into CURRENT behavior.** A roadmap item, a comment describing a future intention, or a partially-wired feature must never be described in `docs/user/` or `docs/developer/` as if it already works.

## Labeling requirements

Every behavior described in canonical documentation must be labeled, explicitly or by the section it lives in, as one of:

- **CURRENT** — implemented, wired up, and ideally covered by a regression test.
- **DEPRECATED** — still present in code but no longer the recommended path.
- **EXPERIMENTAL** — implemented but not yet stable, or on a branch not yet merged to the main line.
- **PLANNED** — described in a roadmap or note but not present in code. Belongs in a roadmap section, never in main prose.

When in doubt, re-check the code rather than guessing.

## Structural rules

5. **Update existing canonical documentation instead of creating duplicate documents.** Check `docs/README.md`'s index and the relevant subfolder before creating a new file. If a document already covers the subject, edit it.
6. **Never rewrite an accepted ADR merely because the implementation evolved.** Write a new ADR that supersedes it, and mark the old one's Status as `Superseded by ADR-NNNN` in both directions.
7. **Keep user documentation separate from internal architecture documentation.** `docs/user/` explains what GitPet does and how to use it, in GitPet's own vocabulary first. `docs/developer/` explains how the code is built, in implementation terms.
8. **Distinguish DEV, installed, and portable builds explicitly wherever the distinction matters** — for example, self-update only runs for installed builds; the DEV executable is filename-prefixed and lives in its own LocalAppData subfolder. Don't let these blur together in prose.
9. **Distinguish GitPet's own application releases from a user project's "Major Update" milestones.** These are two unrelated features that both involve branches, tags, and GitHub Releases, and are the single easiest thing in this codebase to conflate. Any document touching either must state clearly which one it means.

## Version-independence rule

10. **Keep the root `README.md` version-independent.** It must not hard-code a "current version" number, a version-specific architecture heading, or duplicate long-form internal documentation that belongs under `docs/`. The authoritative source for "what is the latest version" is the project's GitHub Releases page, not a string baked into a Markdown file that will inevitably drift (see the post-audit delta in `docs/internal/DOCUMENTATION_AUDIT.md` for a concrete example of what happens when it isn't). A version number is fine in a historical record (`docs/history/RELEASE_HISTORY.md`, an ADR's `Date`), never in living prose that describes current behavior.

## Safety and content hygiene

11. **Keep personal machine paths, credentials, and other private data out of public documentation.** Do not put the maintainer's real file paths (e.g. a real Dropbox or drive-letter path), account names, tokens, or machine-specific detail into committed documentation. Use placeholders (`<repo-root>`, `%LOCALAPPDATA%\ZomniverseGitPet\...`) instead.
12. **Prefer relative repository paths in documentation and relative Markdown links between docs.**
13. **Keep screenshots optional rather than necessary for understanding.**
14. **Document destructive or externally visible behavior explicitly.** Anything that can lose local work, rewrite history, publish data outside the user's machine, or block an action the user expects to succeed must be named plainly, with the exact code path and the exact condition under which it happens.
15. **Document important failure modes** — network failures mid-Send, merge conflicts, a dirty tree during Get, expired GitHub auth, a mismatched allow list — as thoroughly as the happy path.

## Change discipline

16. **Document any new persistent configuration or storage change in the same change that introduces it** — a new `config.json` field, a new file under `%LOCALAPPDATA%\ZomniverseGitPet\`, a schema version bump, or a change to an on-disk format's shape, including what happens to existing data that doesn't fit the new shape.
17. **Update documentation after architectural changes, not just after feature changes.** A change to how a major subsystem works deserves a documentation re-read and correction, not a deferral.
18. **Update `docs/README.md` whenever a canonical document is added or retired.** The entry point must never list a document that no longer exists, or omit one that does.
19. **Audit documentation drift before public releases.** Before a release is published, re-check that the documents describing the subsystems that changed since the last release still match the code, and that no version number was accidentally reintroduced into `README.md` or an architecture heading.

## Public documentation promotion

Canonical documentation must be visible from the repository's default public branch, normally `main`. Users should not be expected to switch to a development branch to find the manual.

After a documentation maintenance pass:

1. Determine the repository's default branch.
2. Compare the canonical documentation on the working/development branch with the default branch.
3. If the documentation is newer, prepare a **documentation-only** promotion from the current default branch.
4. Never merge an entire feature/development branch into `main` merely to publish documentation.
5. A documentation promotion should normally contain only `README.md`, `docs/**`, and other clearly documentation-only files such as `mockups/pet/README.md` when relevant.
6. Do not include application source, tests, build scripts, installer changes, binaries, or unrelated commits unless explicitly approved.
7. Verify `docs/README.md` exists on the public branch, the root README links to it, every referenced document exists, relative links resolve, and Mermaid blocks use GitHub-compatible syntax.
8. Do not promote development-only claims as stable public behavior. Keep them on the development branch or mark them EXPERIMENTAL / UNRELEASED when explicitly approved.
9. Prefer a dedicated docs-promotion branch and pull request so the final diff can be reviewed independently of application development.
10. Never merge that pull request automatically unless the maintainer explicitly asks for the merge.

## Style

- Write concise technical English. Prefer diagrams (Mermaid, GitHub renders them natively) and small, concrete examples over long prose.
- Use consistent terminology — see `docs/reference/TERMINOLOGY.md` — and use it exactly, everywhere.
- Avoid marketing language in developer documentation.
- User documentation should explain Git concepts using GitPet's own terminology first (Save, Get, Send, Reconcile, Checkpoint, Project), with the underlying Git terminology (commit, pull, push, merge, repository) given secondarily where it helps a reader who already knows Git.
- Safety documentation should be explicit and unemotional: state what GitPet may do, what it refuses to do, and where it requires confirmation — as plain fact, not reassurance.

## ADR format

```text
# ADR-NNNN — Title

Status: Accepted
Date: YYYY-MM-DD

## Context
## Decision
## Reasons
## Consequences
## Alternatives considered
```

Numbers are sequential and never reused. ADRs describe *why* a decision exists, not a full implementation manual — link to the relevant developer document for the mechanics.
