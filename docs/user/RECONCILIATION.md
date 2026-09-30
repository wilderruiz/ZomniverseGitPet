# Reconciliation

Reconciliation is the workflow for a repository whose local and online histories have both moved forward independently. GitPet calls that state **diverged**. It does not guess which history should win.

The Reconcile Inspector is the review-first layer for understanding that divergence before repository state is changed.

## Reconcile Inspector

Rows in the **RECONCILE — HISTORIES** workboard can be opened directly when their state is:

- `LOCAL`
- `REMOTE`
- `BOTH SIDES`
- `CONFLICT`

Opening a row is read-only. GitPet pins the relevant revisions and shows the selected file without starting a merge.

The Inspector provides:

- **Summary** — decision-first evidence: change relationship, overlap status, merged-preview status, and pinned revisions;
- **BASE ↔ LOCAL** — what LOCAL changed from the common ancestor;
- **LOCAL ↔ REMOTE** — the two current histories side by side;
- **BASE ↔ REMOTE** — what REMOTE changed from the common ancestor;
- **Merged** — LOCAL before merge beside GitPet's generated three-way candidate.

The code panes always contain source code, not semantic commentary. **Pretty** is display-only; **Exact** preserves source whitespace. Copy/export uses raw backing source.

### What the Summary means

Useful deterministic signals include:

- **INDEPENDENT CHANGES** — LOCAL and REMOTE changed different BASE locations;
- **NO OVERLAP DETECTED** — the parsed BASE-coordinate hunks do not overlap;
- **CLEAN THREE-WAY MERGE** — Git's textual merge engine produced a candidate without conflict markers;
- **REVIEW REQUIRED** / **OVERLAP DETECTED** — the file needs closer inspection.

These signals support a decision; they are not a claim that the program is logically correct.

## Correcting source before reconciliation

### Edit LOCAL

**Edit local** opens the pinned LOCAL source in Exact mode. You can:

- **Validate edit**;
- **Write local** — update only the working file, without staging or committing;
- **Commit local** — create a new local correction commit containing that exact file;
- cancel and keep repository state unchanged.

GitPet refuses a stale LOCAL edit if HEAD or the working file changed after the Inspector snapshot.

### Edit REMOTE

**Edit remote** never edits `origin/<branch>` in place.

**Prepare remote** creates a temporary worktree and temporary local branch based on the inspected REMOTE commit, writes the correction there, and creates a correction commit. The primary working tree and HEAD stay unchanged.

**Send remote** is separate. Immediately before sending, GitPet checks the live remote branch tip again. If REMOTE moved, Send is blocked. The push is a normal fast-forward push; GitPet does not force-push.

**Discard remote** removes the temporary correction state without changing the remote.

### Edit merged candidate

The generated merged candidate starts read-only. **Edit candidate** keeps the generated candidate immutable on the left and opens an editable draft on the right.

**Accept + reconcile**:

1. revalidates the pinned histories and live REMOTE;
2. starts the normal `--no-commit` reconciliation;
3. writes/stages the approved candidate for that selected file;
4. leaves `MERGE_HEAD` present;
5. does **not** create the reconciliation commit;
6. does **not** Send anything.

The normal **Save** action remains the final local commit gate.

## Validation

LOCAL, REMOTE, and merged-candidate drafts can use two deterministic layers before human review:

1. **source checks** — JSON/XML parsing, CSS structural checks, and local PHP/JavaScript/Python/PowerShell parsers when the corresponding tool is installed;
2. **saved Test Commands** — the active project's configured tests, executed in order in an isolated disposable worktree containing the proposed source.

A deterministic syntax/structure failure blocks mutation. A saved project test failure is shown prominently, but it does not silently become the decision-maker: GitPet asks whether you want to continue anyway.

**Stop check** cancels validation while preserving the draft.

## Copy and sensitive paths

The Copy menu can export selected exact source, changed raw lines, a whole source file, a comparison, or structured **Copy everything** Markdown/plain text.

If the path matches GitPet's configured sensitive-path rules (for example a credential/key/token-style path), GitPet asks for explicit approval before placing source on the clipboard.

## Large files

For very large source files, GitPet switches the code panes to **EXACT PERFORMANCE MODE**. The source remains complete and copy/export still uses the full raw text, but expensive Pretty layout, syntax coloring, and changed-line painting are skipped to keep the UI responsive.

## Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+1` | Summary |
| `Ctrl+2` | BASE ↔ LOCAL |
| `Ctrl+3` | LOCAL ↔ REMOTE |
| `Ctrl+4` | BASE ↔ REMOTE |
| `Ctrl+5` | Merged |
| `Ctrl+P` | Pretty / Exact |
| `Ctrl+Shift+C` | Copy everything (Markdown) |
| `Ctrl+Enter` | Validate the active LOCAL / REMOTE / candidate draft |
| `F6` | Move focus between the two source panes |
| `F11` | Maximize / restore the Inspector |
| `Esc` | Stop an active validation or cancel the active unprepared draft edit |

## Starting and finishing the actual reconciliation

The top-level Reconcile action remains explicit. GitPet uses:

```text
git merge --no-commit --no-ff origin/<branch>
```

If unresolved files remain, the established per-file **Keep my local version / Keep online version** resolver is still available. The Inspector adds a more precise option for files you reviewed and edited before or during that process; it does not silently resolve the rest.

When the reconciliation is ready, GitPet still waits for **Save**. Save creates the reconciliation commit locally. Nothing is uploaded until you separately choose **Send**.

Cancelling an active reconciliation runs `git merge --abort` and restores the pre-reconciliation merge state.

See [Reconciliation Safety](../safety/RECONCILIATION_SAFETY.md) for the exact guarantees.
