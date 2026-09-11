# Application lifecycle

Source: `src/ZomniverseGitPet/Program.cs`, `ZomniverseGitPetContext.cs`.

## Startup sequence

1. **Single-instance guard.** `Program.cs` computes a SHA-256-hashed named `Mutex`, keyed off the current Windows user's SID. If GitPet is already running, a named-pipe message activates the existing instance instead of starting a second one.
2. **Splash.** `StartupSplashForm` shows immediately (a small animated splash with staged status text) while slower services initialize.
3. **Load state.** `AppConfig`/`ConfigStore` is loaded; `AuditLog` and `GitService` are constructed; `LogicalProjectScopeRuntime` and `ConnectionUiRuntime` are initialized.
4. **First run.** If `config.OnboardingCompleted` is false, `FirstRunSetupForm` runs (with a guide `PetForm` bubble); cancelling it exits the app rather than continuing into a half-configured state.
5. **Background runtimes start:** `GuardianRemoteWatcher` (polls the remote), `GuardianWorkboardRuntime` (drives the four-quadrant workboard), `StandaloneProjectPublishingUiRuntime` (swaps in the scoped Send button where relevant).
6. **Update check.** `ApplicationUpdateCoordinator.Start()` — a no-op for DEV/portable builds (see below).
7. **Main loop.** `ZomniverseGitPetContext` (an `ApplicationContext`) becomes the app's `MainForm` owner: it creates the single `PetForm`, creates `GuardianForm` on demand, and runs its own poll timer for the (opt-in, off by default) automatic-checkpoint feature.

## DEV vs. installed vs. portable

There is no compile-time flag and no environment variable distinguishing these — the distinction is entirely by **filename convention and one sentinel file**:

| Build | How it's detected | Consequence |
| --- | --- | --- |
| DEV | `Application.ExecutablePath`'s filename starts with `DEV-` (case-insensitive) | Guardian window title becomes "DEV-ZGitPet Guardian" instead of "ZomniverseGitPet Guardian"; self-update never runs (see below). |
| Installed (via the Setup installer) | A sibling `unins000.exe` file exists next to the running executable | `ApplicationUpdateService.IsInstalledBuild` is true; self-update runs. |
| Portable | Neither of the above | Self-update never runs; the user updates by downloading a newer portable build themselves. |

DEV and installed/portable builds share the same `%LOCALAPPDATA%\ZomniverseGitPet\` configuration, audit log, and per-project state — there is no separate DEV data directory. See [`publish-local.ps1`](BUILD_AND_RELEASE.md#the-dev-loop-publish-localps1) for how a DEV executable actually gets produced and named.

See [ADR-0007](../adr/ADR-0007-DEV-VS-INSTALLED-BUILD-DETECTION.md) for why this convention was chosen over compile-time flags.
