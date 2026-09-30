# Reconciliation safety

The exact guarantees behind [Reconciliation](../user/RECONCILIATION.md).

## Inspection is read-only

Opening Reconcile Inspector, switching tabs, scrolling, changing Pretty/Exact presentation, maximizing/restoring, copying approved source, or generating a merged preview does not merge, checkout, stage, commit, reset, clean, stash, or push the repository.

BASE / LOCAL / REMOTE are pinned source identities. BASE is immutable historical evidence.

## No silent reconciliation commit

The real reconciliation uses:

```text
git merge --no-commit --no-ff origin/<branch>
```

The `--no-commit` boundary is deliberate. GitPet creates the reconciliation commit only when the user later presses **Save**. Candidate acceptance can prepare/stage an approved file inside the merge, but it deliberately leaves `MERGE_HEAD` present and does not commit.

Nothing in reconciliation or candidate acceptance performs Send.

## LOCAL correction safety

LOCAL edits are validated against the pinned LOCAL commit and current working file before write. If HEAD moved or the working file changed after the Inspector snapshot, GitPet refuses to overwrite it.

**Write local** changes only the working file. **Commit local** stages only the exact inspected path and creates a new commit; it does not rewrite existing history and does not Send.

## REMOTE correction safety

REMOTE editing is isolated from the primary worktree.

Preparation creates a temporary worktree/branch based on the pinned REMOTE commit. Before Send, GitPet reads the live branch tip with `ls-remote` and requires it to equal the inspected REMOTE SHA.

If REMOTE moved:

- Send is blocked;
- no force push is attempted;
- the new remote history is left untouched;
- the primary working tree/HEAD remain untouched;
- the prepared local correction can be reviewed/discarded.

A normal non-fast-forward rejection remains a second race barrier if the remote moves after the explicit tip check but before the push reaches the server.

## Merged-candidate integrity

An editable merged candidate is separate from the generated candidate evidence.

Before acceptance GitPet verifies, among other things:

- current branch;
- LOCAL HEAD;
- local `origin/<branch>` tracking SHA;
- live REMOTE SHA;
- clean working tree;
- no existing `MERGE_HEAD`;
- regenerated merged candidate identity;
- absence of unresolved Git conflict-marker blocks.

Acceptance starts the normal no-commit merge, verifies `MERGE_HEAD` is the pinned REMOTE commit, writes/stages only the approved selected path, and then stops for the normal Save gate.

If application fails after merge start, GitPet attempts `git merge --abort`.

## Validation is isolated

Language checks and saved Test Commands run against the proposed source in a disposable Git worktree. Candidate validation can reproduce the LOCAL+REMOTE merge in that disposable worktree before inserting the proposed candidate.

Validation cancellation preserves the editor draft. Cleanup is attempted in `finally`, including `git worktree remove --force` and `git worktree prune`.

A deterministic syntax/structure failure blocks the mutation. Saved project tests are evidence rather than absolute authority: a failure is shown to the user and requires an explicit decision before a mutation can continue.

## Sensitive source export

Paths matching configured suspicious-path rules require explicit review before Inspector copy/export actions put source on the clipboard. This applies to selected source, changed blocks, whole files, comparisons, and **Copy everything**.

## Large source handling

Large-file performance mode changes presentation only. It skips expensive Pretty/syntax/change-color passes but keeps the full source text as the backing code. It does not truncate or alter the file.

## Cancelling and recovery

Cancelling an active reconciliation uses `git merge --abort`.

Additional recovery rules include:

- candidate failures after merge start abort the merge;
- cancelled validation cleans its disposable worktree;
- REMOTE Discard/shutdown cleanup removes prepared temporary worktree/branch state;
- no workflow introduces automatic force push, reset, clean, hidden stash, or history rewriting.

## Established whole-file conflict resolver

For unresolved Git conflicts, the established reconciliation dialog can still choose the complete LOCAL or REMOTE version of a file using `checkout --ours/--theirs` (or `git rm` when the chosen side deleted the file).

That is no longer the only review capability: Reconcile Inspector can inspect/edit LOCAL, REMOTE, or an explicit merged candidate before the final Save. Unreviewed files are never silently line-merged by GitPet.

## What reconciliation does not do

- It does not push automatically.
- It does not force-push.
- It does not silently commit.
- It does not silently resolve every conflict.
- It does not start when unrelated unsaved working-tree changes make the operation unsafe.
