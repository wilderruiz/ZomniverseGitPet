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

Several logical projects may also use the **exact same physical repository root**. This is useful for a shared web root such as `public_html`: one project can select `millenova_config.php` + `millenova/`, while another selects its own config file + subdomain folder. The repository is shared; project identity comes from the GitPet project ID and selected scope, not exclusive ownership of the root folder.

Use **Projects → Add project in current repository…** to create another sibling project without creating another `.git` folder or moving files.

## What stays independent between projects

- Display name
- Scope (which files/folders belong to it)
- Its own saved test commands
- Its own [Advanced Project Allow List](ALLOW_LISTS.md), if you save one
- Its own standalone-publishing link, if you set one up (see [Publishing Architecture](../developer/PUBLISHING_ARCHITECTURE.md))
- Its own selected standalone remote branch and branch-specific publish baselines

What's shared: the underlying Git commit history, working tree, and `origin` remote (unless a project publishes standalone — below).

## Getting and sending a scoped project

A whole-repository project uses the ordinary Git history for Get and Send.

A scoped logical project uses its **standalone project remote** in both directions:

- **Branch ▾** chooses which existing branch of the standalone project remote this logical project follows. Changing it does **not** switch the parent repository branch.
- **Send** builds an isolated copy containing only that project's files and pushes the isolated project history to the selected standalone branch.
- **Get** fetches that same selected standalone branch into GitPet's isolated workspace, verifies every incoming changed path is inside the configured project scope, and then copies only those changed project files into the parent working tree.
- Incoming Get files remain **unsaved local changes** so they can be reviewed before Save.
- GitPet never pulls the standalone project's Git history into the parent repository.
- If saved local project updates and incoming standalone updates both exist, Get and Send pause instead of guessing which side should win.

See [Publishing Architecture](../developer/PUBLISHING_ARCHITECTURE.md) and [Project Boundaries](../safety/PROJECT_BOUNDARIES.md) for the isolation and publishing-contract checks.
