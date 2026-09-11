# Advanced Project Allow Lists

An **Advanced Project Allow List** is a plain-text list of the files and folders that belong to a [logical project](LOGICAL_PROJECTS.md), typed or pasted instead of clicked through the scope tree. It's saved so you don't have to redo it every time you reopen the project — and once saved, it becomes the project's publishing contract (see [Project Boundaries](../safety/PROJECT_BOUNDARIES.md)).

## Where it lives

Each logical project's allow-list text is stored separately, per project, so opening a different logical project — even one sharing the same physical repository — restores *its own* allow list, not another project's. See [AppData Layout](../reference/APPDATA_LAYOUT.md) for the exact file.

## Allow-list source vs. resolved scope

The allow list you type is a *source description* — folder paths, file paths, comments (`#`). GitPet resolves that source text against the repository's committed files (as of the last Save) to produce the actual set of leaf files it covers. Two different-looking allow lists can resolve to the same files (a folder entry expands to every file under it); GitPet always compares against the resolved, expanded file set, never the raw text.

Lines are rejected, with a specific reason each, if they:

- point outside the project's root,
- point inside `.git`,
- point into a nested Git repository, or
- don't exist on disk.

## What it's for

Once saved, the allow list is what [Compare with Send](../safety/PROJECT_BOUNDARIES.md) checks against before a scoped project is allowed to publish — so if you add a new folder to the project's scope but forget to update the allow list (or the reverse), GitPet can tell you exactly which files don't match before anything is sent anywhere.

An allow list is optional. If a logical project never has one saved, this extra check simply doesn't run for it.
