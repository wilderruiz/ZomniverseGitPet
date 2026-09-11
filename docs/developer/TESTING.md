# Testing

Source: `tests/ZomniverseGitPet.Tests/` (11 files: `ZomniverseGitPet.Tests.csproj`, `Program.cs`, and 9 regression files).

## Not a standard test framework

There is no xUnit, NUnit, or MSTest package referenced anywhere in `ZomniverseGitPet.Tests.csproj`. The test project is a plain `net8.0-windows` **console executable** that references the main application project directly. Two mechanisms coexist:

- **`Program.cs`** implements a bespoke `Check(name, Func<bool>)` / `CheckAsync(name, Func<Task<bool>>)` runner: each check catches its own exceptions and records a named failure; at the end the process prints a pass/fail summary and returns a non-zero exit code if anything failed.
- **The other 9 files** are each an `internal static class` with a `[ModuleInitializer]`-attributed `Run()` method, which the CLR executes automatically the moment the assembly loads — before `Program.cs`'s own checks even start. These throw a plain exception on failure rather than returning a bool.

"Running the tests" means running the compiled test executable and checking its exit code / printed summary — there is no `dotnet test` attribute-discovery step, and no separate output artifact beyond console text and the process exit code.

See [ADR](../adr/README.md) — a candidate ADR for this choice is tracked but not yet written up in full; see the audit for context.

## What's covered

The regression files exercise, among other things: project allow-list persistence and resolution, project-scope resolution and nested-repository exclusion, the publish-boundary comparison (matched/allow-list-only/send-only), standalone-publishing workspace isolation and content fingerprinting, `.gitignore` suggestion/composition, the Save preflight's separation of normal vs. ignored files, GitHub repository-address parsing and name suggestion, version comparison and SHA-256 verification for self-update, and release-package provenance validation (including that a tampered asset is rejected even if the manifest still claims a matching hash).

Do not rely on a specific "N tests passed" count in any document — it has changed release to release (see `CHANGELOG.md`'s per-version entries) and will keep changing; read the current test files directly if the exact count matters.

## Running the suite

```powershell
dotnet build ZomniverseGitPet.sln -c Release
dotnet run --project tests/ZomniverseGitPet.Tests/ZomniverseGitPet.Tests.csproj -c Release
```

`scripts/build-release.ps1` runs this same test project as a required step before it will build a release package (skippable only with an explicit `-SkipTests` flag, which should never be used for a real release).
