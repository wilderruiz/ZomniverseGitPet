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
    └── UPDATE_SUMMARY.md
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

A short, user-facing summary of the visit. Release packaging reads this file and places the summary into `release-manifest.json`, allowing installed copies of ZGit Pet to explain the new version before the user chooses **Install update**.

Keep it compact enough for a desktop update dialog. The full medical chart belongs in the deworming report.

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
5. release regressions pass;
6. the packaged update manifest contains the correct visit summary and links.

The joke is veterinary. The provenance is not.
