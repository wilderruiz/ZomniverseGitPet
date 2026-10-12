# ZGit Pet Release Clinic

Welcome to the medical archive for the animal.

Every public ZGit Pet version is treated as a **vet visit**. The language is intentionally playful, but the records underneath it are real engineering provenance: bugs, failure modes, root causes, treatments, safety rules, regression evidence, and release outcomes.

## The chart

```text
release/
├── README.md
├── VET_PASSPORT.md
└── v0.6.0/
    ├── DEWORMING_REPORT.md
    ├── UPDATE_SUMMARY.md
    └── GITHUB_RELEASE_NOTES.md
```

For future releases, add a new `v<version>/` folder rather than rewriting an older visit.

### `DEWORMING_REPORT.md`

The heavy technical chart. Each important defect or risk is documented as a parasite specimen with:

- symptoms;
- root cause;
- affected components;
- treatment;
- safety/regression evidence;
- final outcome.

This file is intentionally much more detailed than the normal changelog.

### `UPDATE_SUMMARY.md`

A short, user-facing summary of the visit. Starting with v0.6.0, installed GitPet builds try to read this file directly from the **immutable release tag** when offering a newer version. If the tagged clinic summary does not exist, the updater falls back to the normal GitHub Release notes.

Keep it compact enough for a desktop update dialog. The full medical chart belongs in the deworming report.

### `GITHUB_RELEASE_NOTES.md`

The recommended GitHub Release body for that version. It contains the user-facing visit summary plus the full-record/passport links and download guidance.

This is particularly important for the **first** clinic release: an installed v0.5.0 copy cannot retroactively gain the v0.6.0 updater UI, but it already displays GitHub Release notes. Publishing v0.6.0 with this body therefore gives old clients the same meaningful treatment summary while they upgrade.

### `VET_PASSPORT.md`

The cumulative passport. It records vaccinations/capabilities and notable parasites removed across releases, with version, date, treatment, and outcome.

## Historical policy

The clinic record system itself was introduced while preparing **v0.6.0**. Earlier versions therefore receive retrospective passport summaries from the existing changelog/release history rather than invented per-bug treatment charts.

Starting with v0.6.0, every release should have its own visit folder.

## Release rule

A release is not considered fully charted until:

1. its version folder exists;
2. the deworming report is technically current;
3. the update summary is suitable for users;
4. the cumulative vet passport is updated;
5. the GitHub release-notes file is ready;
6. release regressions pass;
7. the published GitHub Release body carries the correct visit summary for legacy updater compatibility.

## Update-generation compatibility

- **v0.5.0 → v0.6.0:** the already-installed v0.5.0 binary keeps its original popup layout and reads the GitHub Release body. Use `v0.6.0/GITHUB_RELEASE_NOTES.md` as that body.
- **v0.6.0 → future releases:** the new updater first reads the offered tag's `UPDATE_SUMMARY.md`, displays the veterinary update UI, and links directly to the tagged deworming report and passport. GitHub Release notes remain the compatibility fallback.

The joke is veterinary. The provenance is not.
