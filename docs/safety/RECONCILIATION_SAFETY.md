# Reconciliation safety

The exact guarantees behind [Reconciliation](../user/RECONCILIATION.md).

## No silent merge commit

Reconciliation runs `git merge --no-commit --no-ff origin/<branch>` — the `--no-commit` is deliberate: Git prepares the merge (including resolving any conflicts you choose) but never finalizes it as a commit on its own. GitPet only creates the actual reconciliation commit when you press **Save**, with the message `reconcile local and online: <timestamp>`. There is no code path where a merge commit is created without that explicit Save.

## Conflict resolution is per-file and binary

For each conflicting file, GitPet offers exactly two choices — **keep my local version** or **keep the online version** (`git checkout --ours`/`--theirs`, or `git rm` if the chosen side deleted the file) — never a line-level manual merge inside GitPet. There is no ambiguity about what "keep mine" means: it's the whole file, from one side.

## Cancelling is always safe

If reconciliation is cancelled at any point — before Save, whether or not conflicts were involved — GitPet runs `git merge --abort`, which restores the repository to exactly its pre-reconciliation state. The same happens if any step fails partway through. Automatic saving is suspended for the duration of a reconciliation so it can't interfere.

## What reconciliation does not do

- It does not push anything. Sending a reconciliation commit is a separate, later Send.
- It does not touch files outside the ones Git itself reports as changed by the merge.
- It does not run if the working tree has unsaved changes — GitPet asks you to Save first, so an interrupted reconciliation is never confused with in-progress unrelated edits.
