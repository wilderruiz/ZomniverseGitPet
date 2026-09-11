# ADR-0007 — Detect DEV vs. installed vs. portable builds by filename/sentinel file

Status: Accepted
Date: 2026-09-11

## Context

Several behaviors need to know which of GitPet's three distribution forms is currently running — most importantly, whether self-update should run at all (see [ADR-0006](ADR-0006-INSTALLED-ONLY-SELF-UPDATE.md)) and whether the Guardian window should visibly mark itself as a development build.

## Decision

There is no compile-time flag (`#if DEBUG`/`RELEASE`) and no environment variable for this. Instead: a DEV build is detected by the running executable's filename starting with `DEV-` (set by `publish-local.ps1` when it copies the build out); an installed build is detected by the presence of a sibling `unins000.exe` file next to the executable (placed there by the Inno Setup installer). Anything that is neither is treated as portable.

## Reasons

- A filename convention and a sentinel file both survive exactly as long as the actual distribution artifact does — there's no separate flag to keep in sync with how the executable was actually built or deployed.
- Avoids maintaining two build configurations (Debug/Release-for-DEV vs. Release-for-shipping) that differ only in this one behavior; the same Release build works for all three distribution forms, and the *deployment* — which script copied it where, whether an installer ran — is what determines the answer.
- Detection works correctly even if a DEV or portable executable is copied somewhere unexpected, since it only depends on the file's own name and its immediate neighbors, not on where it happens to be running from.

## Consequences

- Renaming a DEV executable to drop the `DEV-` prefix would cause it to stop being detected as a DEV build (and would then attempt self-update if it also happened to sit next to an `unins000.exe`, which is not something either build script does) — this convention is only as reliable as the naming discipline enforced by the build scripts, not something the runtime can independently verify.
- Any future third-party packaging (an MSI, a Windows Store package, etc.) would need to either replicate the `unins000.exe` sentinel or gain its own explicit detection branch.

## Alternatives considered

- **Compile-time `#if` flags** — rejected: would require maintaining separate build configurations that produce otherwise-identical binaries, purely to flip this one behavior.
- **An environment variable** — rejected: easy to set accidentally or leave stale in a developer's shell profile, and provides no signal at all for the installed-vs-portable distinction, which still needs a real, on-disk signal either way.
