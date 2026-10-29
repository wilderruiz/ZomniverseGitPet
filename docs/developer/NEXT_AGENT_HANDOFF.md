# ZGitPet Living Next-Agent Handoff

**Updated:** 2026-10-29

## Current checkpoint

The Chat Vitality engine is being upgraded from a single deterministic load score to a cross-repository empirical lifespan model. Repository births remain independent: a new birth closes only the preceding active interval for the same repository.

## Changed scope

- `src/ZomniverseGitPet/ChatVitality.cs` — repository-aware completed observations, per-repository and pooled statistics, current age, provisional empirical bands, observable activity pressure and nondeterministic guidance.
- `src/ZomniverseGitPet/ChatVitalityControl.cs` — displays empirical age/band instead of presenting the heuristic score as a universal lifespan truth.
- `tests/ZomniverseGitPet.Tests/ChatVitalityRegression.cs` — cross-repository isolation, Millenova seed attribution, pooled statistics and nondeterministic-output regression coverage.
- `docs/developer/CHAT_VITALITY.md` — architecture and cross-repository same-write handoff policy.

## Evidence and provenance

The initial eight completed observations belong only to `wilderruiz/millenova`: 32.3h, 58.0h, 61.1h, 61.4h, 17.2h, 23.1h, 10.6h and 45.3h. They seed provisional bands and pooled statistics but are not copied into unrelated repository statistics.

## Blockers

No product blocker is known. Build and the full regression executable must pass before Save/Send.

## Exact next action

Run Release build and the full regression project. Inspect the diff for repository-name/path coupling. Then use ZGitPet Save/Send only after acceptance.

## Do not repeat / safety constraints

- Do not move empirical history into a managed repository's transport helper; ZGitPet core owns it.
- Do not close repository A's active interval when repository B announces a birth.
- Do not treat Millenova seed values or provisional color bands as universal facts or a deterministic cap.
- Do not fabricate token/context percentages or predict a death deadline.
- For every future agent-originated remote write, update only each affected repository's living handoff in the same write sequence.
