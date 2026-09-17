# ZomniverseGitPet pet mockups

This design-review pack defines one coherent **purple fox repository companion** for ZomniverseGitPet. The mascot is friendly, intelligent, trustworthy, and guardian-like, with rounded proportions, warm expressions, natural pale muzzle and chest markings, and a large readable tail. Its simple silhouette and consistent construction are suitable for future animation and remain readable at small scale.

## States

- `pet_idle_01.svg` — calm, upright canonical fox and everyday monitoring state.
- `pet_idle_02.svg` — the same fox in a cozy curled pose with a Git branch motif.
- `pet_happy_01.svg` — warm healthy-repository state with a secondary green check.
- `pet_review_ready_01.svg` — attentive state for changes ready to review.
- `pet_warning_01.svg` — protective, softly concerned caution state.
- `pet_sleep_01.svg` — compact resting state for inactivity or paused monitoring.

## Preview

Open `mockups/pet/index.html` directly in a browser. The gallery has no build step, framework, network request, or external dependency.

The generic idle, happy, review-ready and warning images remain embedded for ordinary repository states.

## Save lifecycle assets

Save uses dedicated artwork selected centrally by `PetAssets.ForOperation` from the shared operation state:

| Phase | SVG source |
| --- | --- |
| Preparing | `pet_thinking_01`, `pet_save_prepare_01` |
| Checking path support | `pet_thinking_01` |
| Staging | `pet_save_sorting_01`, `pet_save_sorting_02` |
| Creating checkpoint | `pet_save_packing_01`, `pet_save_packing_02` |
| Completed | `pet_save_success_01` |
| Warning | `pet_save_warning_01` |
| Failed | `pet_save_error_01` |
| Cancelled | Existing idle artwork |

All sources live in `svg/`, use the existing transparent 160 × 160 viewBox, and share the original fox geometry and palette. Sorting and packing alternate at the existing 260 ms UI interval; this conveys activity, not per-file progress. Terminal states retain the existing display hold.

WinForms displays embedded PNGs, so the SVGs are rasterized to transparent 320 × 320 PNGs in `src/ZomniverseGitPet/Assets/Pet`. Run `node scripts/render-pet-assets.cjs` from the repository with the `sharp` Node module available (including via `NODE_PATH`) after editing sources. Commit both SVGs and PNGs. Normal app builds need no SVG renderer. Missing dedicated resources fall back to the closest generic state.

## Visual direction

The fox combines the reference's Elegant Guardian proportions with the Rounded Scout's friendliness and the Bright Helper's warmth. Purple remains the stable identity color; green, gold, and amber are secondary Git-state accents. Rounded cheeks, separate lower-face muzzle patches, soft inner ears, warm eyes, and a broad pale-tipped tail keep the character approachable and recognizable at launcher scale.

## Next step after approval

Approved concept → refined asset set → animation/state specification → app integration.
