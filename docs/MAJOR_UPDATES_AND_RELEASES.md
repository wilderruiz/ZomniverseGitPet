# Major updates, milestones, and releases

ZomniverseGitPet treats a **Checkpoint**, a **major milestone**, and a **GitHub Release** as different layers of the same ordinary Git history.

## The human model

```text
small ongoing edit
    -> Checkpoint

large redesign / data migration / new architecture
    -> Major update? review
    -> preserve previous generation as legacy
    -> continue redesign on a new branch

stable named generation
    -> Git tag
    -> optional GitHub Release
```

GitPet never decides that a project *is* a major update. It looks for signals such as unusually high file/line churn, many additions/deletions, schema/database/dependency changes, older data formats being replaced by JSON/SQL, and local/remote histories that both contain unique commits. When the signal is strong, the Guardian menu changes from **Milestones** to **Major update? ✦**.

## Safe legacy + redesign plan

A suggested v1 -> v2 transition looks like:

```text
published / previous generation
        |
        +-- branch: legacy/v1
        +-- tag:    v1.0.0-legacy

current redesign
        |
        +-- branch: redesign/v2
```

The legacy branch is a living name for the old generation. The tag is an exact frozen marker suitable for a GitHub Release. The redesign branch keeps the new architecture separate until the user deliberately decides how it should become the next `main`.

If local and `origin` have diverged, GitPet prefers the old published `origin` commit as the legacy target and leaves the current local generation intact on the new branch. It does **not** force-push or rewrite `main`.

If the large redesign is still uncommitted, GitPet can mark the current `HEAD` as the legacy baseline, create the redesign branch at that same commit, and switch to it while leaving the working changes on disk. The user can then create the next Checkpoint on the redesign branch.

## Publication is deliberately separate

**Create local release plan** creates only local branches/tags and switches the current work to the requested new branch. It does not contact GitHub.

**Publish legacy refs ↑** is a separate confirmation. It pushes only the protected legacy branch and tag to the already-configured `origin`; it never pushes the redesign branch as part of that action.

The redesign can later be published with the normal Guardian **Push ↑** action.

For GitHub remotes, GitPet provides:

- **Create GitHub Release ↗** — opens GitHub's release editor for the legacy tag and copies suggested title/notes to the clipboard.
- **Previous releases ↗** — opens the repository's Releases archive.

Once a GitHub Release exists, GitHub automatically shows releases in the repository sidebar. GitPet does not rewrite arbitrary project README/About content just to maintain release links.

## Safety boundaries

Major-update support does not change GitPet's core rules:

- no automatic push
- no automatic merge
- no force-push
- no `reset --hard`
- no `clean`
- no automatic remote creation/replacement
- no deletion of previous history
- no GitHub Release publication without the user's explicit browser action

Everything GitPet creates is ordinary Git state: commits, branches, and tags that remain inspectable with normal Git tools.
