# UI architecture

GitPet's UI is plain WinForms — no XAML, no third-party UI framework — built from a small set of shared conventions rather than a component library.

## Theme

`GuardianTheme.cs` centralizes the color palette (a dark violet/pink identity: `Window`, `Surface`, `SurfaceRaised`, `SurfaceSoft`, `Ink`, `MutedInk`, `Violet`, `HotPink`, `Healthy`, `Warning`, `Changes`, `Border`, `Console`, and similar) as static properties, so every form and control pulls from one place rather than hard-coding colors.

## Window chrome

`WindowChrome.ApplyGuardianChrome(form)` applies a consistent custom title-bar treatment across every dialog and window, so `AboutForm`, `GuardianForm`, `ApplicationReleaseForm`, and the rest all look like one application rather than a pile of default WinForms dialogs. `WindowPlacementManager` persists window position/size (`window-layout.json`, see [AppData Layout](../reference/APPDATA_LAYOUT.md)) so windows reopen where you left them.

## Custom controls

| Control | Purpose |
| --- | --- |
| `GuardianActionButton` | The Save/Get/Send/Reconcile-style action buttons; enable state, label, and badge count are all data-driven off `GuardianSyncState.Current` rather than hand-toggled per callsite. |
| `GuardianStatusChip` | Small colored status pills (branch, health, sync state). |
| `PetChromeButton`, `OnboardingButton` | Custom-painted rounded-rectangle buttons (GDI+ `GraphicsPath`) used where a standard WinForms `Button` wouldn't match the theme. |
| `PetMessageBubble` | The speech-bubble style guidance shown next to the desktop pet. |
| `SafeSplitContainer` (aliased in for every `SplitContainer` via `SplitContainerAliases.cs`) | A defensive subclass working around a known WinForms exception when a splitter is briefly given an invalid size (e.g. during a DPI change or fast resize). |

## The Guardian workboard

`GuardianWorkboardControl` renders the four-quadrant SAVE / GET / SEND / RECONCILE layout described in [Save, Get, Send](../user/SAVE_GET_SEND.md) and [Reconciliation](../user/RECONCILIATION.md). `GuardianWorkboardRuntime` polls (roughly every 1.8 seconds) and asks `GuardianWorkboardService` to project the current `git status`/`diff`/`log` output into workboard rows — the workboard never mutates the repository itself, it only reads and displays.

## File Review

`FileComparisonPanel` is a two-pane before/after diff viewer for one file at a time (not a multi-file tree), with a `DiffLineMap` that maps a unified, zero-context diff onto exact changed-line numbers in each pane, and a toggle between a plain-language "Human summary" and the raw "Technical" line-by-line view.
