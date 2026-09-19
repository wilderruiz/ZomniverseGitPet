# Lynx Lab

Lynx Lab is the isolated Windows test host for the next ZGitPet desktop guardian.

The lab currently uses WinForms/GDI+ to render the approved Hairy Guardian as
live vector geometry. The reference artwork is not used as a runtime bitmap.
Direct2D/DirectComposition can replace the backend later without changing the lab controls.

## Run

From the repository root:

```powershell
dotnet run --project experiments\LynxLab\LynxLab.csproj
```

or:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\run-lynx-lab.ps1
```

## Phase 0 acceptance checks

- Lab launches independently from ZGitPet.
- Main viewport renders the Lynx at vector quality.
- The viewport and inspector are separated by a draggable splitter.
- Inspector controls resize with the available width and scroll vertically when needed.
- The main window can be resized without controls overlapping or forcing the canvas size.
- All Git-state simulation buttons change the visual state.
- All eight dark palettes can be selected.
- Debug geometry can be toggled.
- Desktop Preview opens as a separate 240 × 246 transparent window.
- Desktop Preview can be dragged and reset to the bottom-right.
- Closing Lynx Lab closes its preview.
- No production GitPet code or configuration is read or changed.

The renderer is behind `ILynxRenderer`; the intended next backend can therefore
be Direct2D/DirectComposition without changing the lab controls.


## Current live mascot baseline

The default renderer is `HairyGuardianRenderer`.

It preserves the approved direction:

- bright purple head with darker purple body and paws
- fluffy dark tail with purple highlights
- white muzzle and chest
- expressive glossy eyes
- dark armored collar
- glowing shield/check emblem
- state-specific visual signals for Save, Get, Send and Conflict
- palette selection changes undertones while preserving the purple GitPet identity

The older `GdiLynxRenderer` remains in the project as a comparison/fallback renderer.
