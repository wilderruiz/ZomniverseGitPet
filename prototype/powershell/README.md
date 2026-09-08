# ZomniverseGitPet PowerShell prototype

This directory preserves the original PowerShell/WinForms proof of concept as a reference and rollback implementation. The maintained public application is the C# project under `src/ZomniverseGitPet/`.

The prototype supports a floating pet, Guardian dashboard, Git status and diff review, configurable test commands, local restore-point commits, audit logging, and optional verified automatic checkpoints.

Before running it, edit `config.default.json` and replace the example repository path with a local Git working tree. Mutable configuration and audit data are stored beneath `%LOCALAPPDATA%\ZomniverseGitPetPrototype`.

Run `ZomniverseGitPetPrototype.vbs` for a hidden-console launch or `RUN_ZOMNIVERSEGITPET_PROTOTYPE.cmd` for diagnostics.

The prototype is retained for historical comparison and is not the recommended release build. Its synchronous process calls can temporarily block the WinForms UI.

