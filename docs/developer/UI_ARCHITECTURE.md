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
| `PetDirect2DControl` | Production 160 × 160 live Guardian surface. Uses native Direct2D path geometry and state/activity animation; `PetForm` falls back to embedded PNG assets if Direct2D initialization or rendering fails. |
| `SafeSplitContainer` (aliased in for every `SplitContainer` via `SplitContainerAliases.cs`) | A defensive subclass working around a known WinForms exception when a splitter is briefly given an invalid size (e.g. during a DPI change or fast resize). |

## The Guardian workboard

`GuardianWorkboardControl` renders the four-quadrant SAVE / GET / SEND / RECONCILE layout described in [Save, Get, Send](../user/SAVE_GET_SEND.md) and [Reconciliation](../user/RECONCILIATION.md). `GuardianWorkboardRuntime` polls (roughly every 1.8 seconds) and asks `GuardianWorkboardService` to project the current `git status`/`diff`/`log` output into workboard rows — the workboard never mutates the repository itself, it only reads and displays.

## File Review

`FileComparisonPanel` is a two-pane before/after diff viewer for one file at a time (not a multi-file tree), with a `DiffLineMap` that maps a unified, zero-context diff onto exact changed-line numbers in each pane, and a toggle between a plain-language "Human summary" and the raw "Technical" line-by-line view.

## Reconcile Inspector

`ReconcileInspectorPanel` is Guardian's review-first reconciliation workspace. It is the same live control whether embedded in Guardian or moved into the maximized Inspector window, so maximizing does not clone or reset state.

The main supporting components are:

| Component | Responsibility |
| --- | --- |
| `ReconcileInspectorSourceService` | Pins BASE / LOCAL / REMOTE commit identity, loads immutable source, builds BASE-derived diffs, and requests the merged preview. |
| `ReconcileChangeAnalyzer` / `ReconcileSummaryPresentation` | Deterministic change-shape, overlap, and decision-summary presentation. |
| `SharedCodeReviewRenderer` | Shared syntax coloring, Pretty/Exact presentation, changed-line backgrounds, and large-source exact-mode fallback. |
| `RichTextScrollLink` | Optional linked scrolling plus capture/restore of per-view scroll positions. |
| `ReconcileCopyExport` | Raw-source copy/comparison and structured Markdown/plain-text export. |
| `ReconcileLocalEditService` | Stale-safe LOCAL validation/write boundary. |
| `ReconcileRemoteEditService` | Isolated REMOTE worktree/branch preparation, live-tip race checks, non-force Send, and cleanup. |
| `ReconcileMergedCandidateService` | Pinned candidate integrity checks and explicit application into the normal no-commit reconciliation. |
| `ReconcileValidationService` | Disposable-worktree language checks and saved Test Commands. |

### Interaction/state rules

- Comparison geometry is always two columns.
- The generated merged candidate is preserved as immutable evidence when an editable candidate draft is opened.
- The same Inspector instance is reparented for Max/Restore, preserving edits and selection.
- `SafeSplitContainer.PreferredRatio` preserves the user's divider ratio through resize/DPI transitions.
- Reconcile Inspector stores scroll snapshots per comparison tab and restores them after re-rendering that tab.
- Large sources stay complete but bypass Pretty/syntax/change-paint work above the performance threshold.
- Accessible names are assigned to the Inspector, source panes, source identity labels, tabs, status surface, and action buttons.
- Keyboard navigation is handled by `ReconcileInspectorPanel.ProcessCmdKey`: `Ctrl+1…5`, `Ctrl+P`, `Ctrl+Shift+C`, `Ctrl+Enter`, `F6`, `F11`, and safe `Esc` cancellation.

### Mutation boundary

Viewing, tab switching, scrolling, Pretty/Exact, Max/Restore, and merged-preview generation never change repository state.

LOCAL write/commit, REMOTE prepare/send, and candidate acceptance are separate explicit actions, each with stale-state/sensitive-path/validation gates appropriate to that operation.
