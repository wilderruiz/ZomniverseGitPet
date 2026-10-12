# ZomniverseGitPet 0.6.0 — Vet Visit

GitPet came back from a very large deworming visit. 🐾

## What changed

- **Reconcile Inspector** — review pinned BASE, LOCAL and REMOTE source side by side, see overlap/change-shape evidence, and inspect a generated three-way merged candidate before changing Git state.
- **Safer correction surgery** — edit LOCAL without rewriting history, prepare REMOTE corrections in an isolated worktree with moved-remote race protection, or edit the merged candidate while preserving the final Save gate.
- **Better real-conflict treatment** — active textual merge conflicts can now use an explicit merged version instead of forcing the entire LOCAL or ONLINE file to win.
- **Validation booster** — proposed source can be checked in disposable worktrees with JSON/XML/CSS validation, optional PHP/JavaScript/Python/PowerShell parsers, and project-saved Test Commands.
- **Repository care** — first-class GitHub repository creation, sibling logical projects on one repository root, review-first publication workspace, clearer repository links, and safer cross-drive repository moves.
- **Parasites removed** — fixed the Reconcile Copy-menu disposal crash, hidden Edit LOCAL action, broad `token` secret-path false positives, stale REMOTE Send race, unrelated-history reconciliation, Windows path-block/file-lock confusion, validation quoting, shortcut escape from locked editors, and platform-sensitive release regressions.

## Full clinic record

- **Technical deworming report:** https://github.com/wilderruiz/ZomniverseGitPet/blob/v0.6.0/release/v0.6.0/DEWORMING_REPORT.md
- **Veterinary passport:** https://github.com/wilderruiz/ZomniverseGitPet/blob/v0.6.0/release/VET_PASSPORT.md

## Downloads

The **Setup EXE** is the recommended download for normal Windows use. This release also includes the portable build, automatic-update manifest and SHA-256 checksums.

The veterinary language is for fun; the linked treatment record contains the actual engineering details, root causes, safety boundaries and regression evidence.
