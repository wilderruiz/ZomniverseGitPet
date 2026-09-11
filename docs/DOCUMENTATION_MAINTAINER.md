# Documentation Maintainer Contract — ZomniverseGitPet

This file is the standing contract for anyone (human or agent) doing documentation work in this repository from this point forward. It supersedes ad-hoc habits from earlier notes such as `docs/wilder_notes.md`, which is a personal scratch file, not a canonical source.

## Core principle

Documentation must describe the system that **exists**, not the system anyone wishes existed. A doc's job is to be a faithful map of current, checked-in behavior — not a proposal, not a memory of what was intended, not an inference from a class name.

## Before touching any documentation

1. **Inspect the implementation before documenting it.** Read the actual source files and, where they exist, the regression tests that exercise the behavior in question. Never write a claim about behavior based only on a file name, a class name, or a comment's stated intent — comments and names drift from code.
2. **Treat source code and tests as authoritative.** If a doc and the code disagree, the code is right and the doc is wrong (fix the doc). If a doc and a regression test disagree about a behavior the test actually exercises, the test's assertions are the ground truth.
3. **Never invent behavior.** If you cannot verify a claim by reading code or a test, either don't make the claim, or explicitly label it as unverified / planned / aspirational (see labeling rules below). Do not fill gaps with plausible-sounding detail.

## Labeling requirements

Every behavior described in canonical documentation must be labeled, explicitly or by the section it lives in, as one of:

- **CURRENT** — implemented, wired up, and (ideally) covered by a regression test or directly observable in the code path that runs in production use.
- **DEPRECATED** — was implemented, still present in code, but no longer the recommended path (e.g. the PowerShell prototype, superseded config schemas).
- **EXPERIMENTAL** — implemented but not yet stable/complete, or gated behind a flag, or acknowledged in code/comments as provisional.
- **PLANNED** — described in a roadmap, issue, or note, but not present in the code at all. Planned behavior must never appear in `docs/user/` or `docs/developer/` prose as if it already works; it belongs in a roadmap section or a dedicated planning doc, clearly marked.

When in doubt about which label applies, re-check the code rather than guessing.

## Structural rules

4. **Update existing canonical documentation instead of creating duplicate documents.** Before creating a new file, check `docs/README.md`'s index and the relevant subfolder for an existing doc covering the same subject. If one exists, edit it. A second doc on the same topic is a maintenance liability, not a convenience.
5. **Preserve historical architecture decisions through ADRs.** When an intentional architectural trade-off changes, do not edit the old ADR to match the new reality — write a new ADR that supersedes it (state the supersession explicitly in both documents). ADRs are a record of decisions made at a point in time, not living documents.
6. **Keep user documentation separate from internal architecture documentation.** `docs/user/` explains what GitPet does and how to use it, in GitPet's own vocabulary first. `docs/developer/` explains how the code is built, in implementation terms. A user document should never require reading class names to understand; a developer document should never soften a mechanism to protect a beginner's feelings.

## Safety and content hygiene

7. **Avoid leaking secrets, private paths, credentials, or user-specific information.** Do not put the maintainer's real file paths (e.g. an actual `I:\Dropbox\...` path), account names, tokens, or machine-specific detail into committed documentation. Use placeholders (`<repo-root>`, `%LOCALAPPDATA%\ZomniverseGitPet\...`) instead.
8. **Prefer relative repository paths in documentation.** Reference `src/ZomniverseGitPet/GuardianForm.cs`, not an absolute filesystem path.
9. **Keep screenshots optional rather than necessary for understanding.** Prose and small examples should stand on their own; an image may help but a reader without one must not be lost.
10. **Document safety boundaries and destructive operations explicitly.** Anything that can lose local work, force-overwrite history, or send data off the user's machine must be named plainly, in `docs/safety/`, with the exact command or code path that performs it and the exact condition under which GitPet will or won't do it automatically.
11. **Document important failure modes.** What happens when the network is down mid-Send, when a merge conflicts, when the working tree is dirty during a Get, when GitHub auth expires — these are as important to document as the happy path.

## Change discipline

12. **Document any new persistent configuration.** Any new field written to `config.json`, `project-allow-lists.json`, `standalone-publishing.json`, the audit log, or any other file under `%LOCALAPPDATA%\ZomniverseGitPet\` must be added to the relevant reference doc (schema, location, default value, who reads/writes it) in the same change that introduces it.
13. **Document migrations when behavior or storage formats change.** If a config schema version bumps, or an on-disk format changes shape, document what the migration does to existing user data and what happens to entries that don't fit the new shape.
14. **Update `docs/README.md` whenever a canonical document is added or retired.** The entry point must never list a document that no longer exists, or omit one that does.
15. **Audit documentation drift after substantial architectural changes.** After a change that touches how a major subsystem works (not a small bugfix), re-read the docs that describe that subsystem and correct anything that no longer matches, rather than leaving the correction for later.

## Style

- Write concise technical English. Prefer diagrams and small, concrete examples over long prose.
- Use consistent terminology — see `docs/reference/TERMINOLOGY.md` for the canonical GitPet vocabulary, and use it exactly, everywhere.
- Avoid marketing language in developer documentation.
- User documentation should explain Git concepts using GitPet's own terminology first (Save, Get, Send, Reconcile, Checkpoint, Project), with the underlying Git terminology (commit, pull, push, merge, repository) given secondarily where it helps a reader who already knows Git.

## ADR format

```
# ADR-NNNN — Title

Status:
Date:

## Context

## Decision

## Reasons

## Consequences

## Alternatives considered
```

Numbers are sequential and never reused. Never rewrite an accepted ADR merely because the implementation evolved — write a new, superseding ADR instead, and mark the old one's Status as `Superseded by ADR-NNNN`.
