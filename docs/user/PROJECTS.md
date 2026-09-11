# Projects

A GitPet "project" is a folder GitPet is watching, plus a bit of GitPet's own bookkeeping about it (a display name, whether it tracks the whole repository or a smaller scope, and its own saved test commands). Opening a project does not require GitHub — everything here works the same in Local Git Only mode.

## Opening a folder

When you point GitPet at a folder, it inspects it before doing anything:

| What GitPet finds | What happens |
| --- | --- |
| The folder is already the root of a Git repository | GitPet opens it directly. |
| The folder is *inside* an existing Git repository (not the root) | GitPet registers it as a named project scoped to that subfolder, sharing the parent repository's history — see [Logical Projects](LOGICAL_PROJECTS.md). |
| The folder is an ordinary folder, not a Git repository yet | GitPet offers to prepare it (below). It never does this without asking. |
| The folder looks like a Git repository but Git itself can't be run, or reports an error | GitPet explains the problem rather than guessing. |

## Preparing a new project

If a folder isn't a Git repository yet, preparing it walks through:

1. **Choose what belongs to the project** — a tree view where you check the files and folders that should be tracked, or paste a plain-text [Advanced Project Allow List](ALLOW_LISTS.md) instead. Choosing everything is the default and equivalent to "track the whole folder."
2. **Review repository hygiene** — GitPet scans the selected scope for common secrets and generated files (`.env`, private keys, `node_modules`, build output, IDE state, and similar) and proposes `.gitignore` rules, grouped as privacy / generated / system / archive / your own custom rules. You see a before/after preview before anything is written.
3. GitPet then runs the one real Git command this step performs:

   ```bash
   git init -b main
   ```

Preparing a project does **not** create an online repository, configure a remote, stage any files, create the first Save, or Get/Send anything — those are separate, later actions you take deliberately.

## Recent projects

GitPet remembers up to 20 recently used projects so you can switch between them from the Projects menu without re-browsing for the folder each time.

## Related documents

- [Logical Projects](LOGICAL_PROJECTS.md) — when more than one project shares a physical repository.
- [Advanced Project Allow Lists](ALLOW_LISTS.md) — the persisted, editable version of "what belongs to this project."
