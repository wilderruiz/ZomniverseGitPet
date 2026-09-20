# Logical projects

Normally, one Git repository is one project. GitPet also supports the opposite: **several named GitPet projects living inside one physical Git repository**, each tracking its own slice of the folder tree.

```mermaid
flowchart TB
    subgraph REPO["One physical Git repository"]
        direction TB
        RootHistory["Shared commit history"]
    end
    P1["Project A\n(tracks the whole repository)"] --> REPO
    P2["Project B\n(tracks only /packages/widgets)"] --> REPO
    P3["Project C\n(tracks only /docs)"] --> REPO
```

## Why this exists

A single folder on disk sometimes contains more than one thing you want to think of and publish separately — a shared monorepo where you only care about one package, or a personal workspace where a subfolder is really its own small tool. Rather than requiring a separate Git repository (and separate history) for each, GitPet lets you register several named projects against the same repository, each with its own scope.

## How scope works

Each logical project is either:

- **Whole-repository** — tracks everything, the ordinary case, or
- **Scoped** — tracks only the files and folders you explicitly selected, via the project-scope tree or a pasted [Advanced Project Allow List](ALLOW_LISTS.md).

A project's scope does not have to be a single subfolder — it can be an arbitrary, scattered set of files and folders anywhere under the repository root.

## What stays independent between projects

- Display name
- Scope (which files/folders belong to it)
- Its own saved test commands
- Its own [Advanced Project Allow List](ALLOW_LISTS.md), if you save one
- Its own standalone-publishing link, if you set one up (see [Publishing Architecture](../developer/PUBLISHING_ARCHITECTURE.md))

What's shared: the underlying Git commit history, working tree, and `origin` remote (unless a project publishes standalone — below).

## Getting and sending a scoped project

A whole-repository project uses the ordinary Git history for Get and Send.

A scoped logical project uses its **standalone project remote** in both directions:

- **Send** builds an isolated copy containing only that project's files and pushes the isolated project history to its linked remote.
- **Get** fetches that same standalone remote into GitPet's isolated workspace, verifies every incoming changed path is inside the configured project scope, and then copies only those changed project files into the parent working tree.
- Incoming Get files remain **unsaved local changes** so they can be reviewed before Save.
- GitPet never pulls the standalone project's Git history into the parent repository.
- If saved local project updates and incoming standalone updates both exist, Get and Send pause instead of guessing which side should win.

See [Publishing Architecture](../developer/PUBLISHING_ARCHITECTURE.md) and [Project Boundaries](../safety/PROJECT_BOUNDARIES.md) for the isolation and publishing-contract checks.
