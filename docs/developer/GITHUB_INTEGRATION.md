# GitHub integration

Source: `GitHubAccountService.cs`, `GitPetConnectionModes.cs`, `ConnectionSettingsForm.cs`, `ConnectionUiRuntime.cs`, `RemoteSetupForm.cs`, `RepositoryConnectionWizardForm.cs`, `CloneRepositoryForm.cs`.

## No OAuth client, no stored token

GitPet has no custom OAuth implementation and stores no personal access token. All authentication is delegated to the **GitHub CLI (`gh`)**:

- `GitHubAccountService.GetStatusAsync()` runs `gh --version`, `gh auth status`, and `gh api user --jq .login` to derive current auth state fresh each time — GitPet doesn't cache or persist it itself.
- Sign-in launches `gh auth login --web --git-protocol https` in a visible window, followed by `gh auth setup-git`; GitPet then polls `GetStatusAsync` (every second, with a timeout) until it succeeds.
- Sign-out/account switch runs `gh auth logout [--user <login>]`.
- Multiple accounts are `gh`'s own concern — GitPet only ever tracks "the currently active `gh` login."

See [ADR-0001](../adr/ADR-0001-GITHUB-CLI-AUTHENTICATION.md) for why.

## Connection mode

`GitPetConnectionModes` defines three states: `unconfigured`, `github`, `local-git-only`, stored as `AppConfig.ConnectionMode`. This is a **UI/policy label only** — `GitService` operations never branch on it. `ConnectionUiRuntime` polls GitHub auth status (roughly every second while first-run sign-in is pending, every five seconds otherwise) to keep the toolbar's connection pill current.

## Repository connection flows

| Form | When it's used | What it does |
| --- | --- | --- |
| `RemoteSetupForm` | Any Git host, GitHub or not | Paste a URL, add it as `origin`. Refuses to overwrite an existing `origin`. |
| `RepositoryConnectionWizardForm` | GitHub-authenticated | Three branches: **exists already** (paste owner/repo or URL), **create it for me** (`gh repo create`, name suggested from the folder name), **I'm not sure** (`gh repo list` + a similarity score against the local folder name, exact > substring > unrelated). None of the three branches pushes anything — they only resolve the remote URL. |
| `CloneRepositoryForm` | Cloning an existing repository into a new folder | Validates the destination is empty; actual clone runs through `GitServiceCloneExtensions.CloneRepositoryAsync`. |

## Repository creation

`GitHubAccountService.CreateRepositoryAsync` runs `gh repo create owner/name --private|--public [--description ...]` and returns the resulting clone URL — it creates the repository only, it never pushes as a side effect.
