# Lynx Lab

Lynx Lab is the isolated Windows test host for the next ZGitPet desktop guardian.

Phase 0 deliberately uses only WinForms/GDI+ so the laboratory itself can be
validated before Direct2D/DirectComposition or animation dependencies are added.

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
