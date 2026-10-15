# Chat Vitality

Chat Vitality is a local, repository-scoped record of coding-chat generations and observable repository activity. It estimates **session load / handoff risk**. It does not know or claim hidden token counts, context remaining, model limits, or remaining lifespan.

## Persistence and privacy

Data is stored atomically in `%LOCALAPPDATA%\ZomniverseGitPet\chat-vitality.json`, following GitPet's established local JSON-store pattern. This established application persistence mechanism was chosen instead of adding SQLite and a new dependency for two append-sized collections. Nothing is written into a managed project repository and the feature makes no network calls. A SHA-256 digest of the normalized repository path is the repository key; the local path is retained only in GitPet's own private state.

Each session records its generation, start/end timestamps, birth commit, optional death-anchor commit, outcome, operation counters, observed changed-file/churn counts, elapsed minutes, and current heuristic score. Events record completed Get, Save, Send, and Reconcile operations. Commits are anchors in repository history; commits themselves never become "dead."

## Lifecycle

Use **+ NEW CHAT** in the Guardian header widget to register a birth explicitly. GitPet never assumes that Get means a new chat.

1. GitPet reads the current repository `HEAD` as the birth anchor.
2. Generation increments independently for that repository.
3. If an active previous generation exists, select `CLEAN_HANDOFF`, `HARD_HANG`, `MAX_LENGTH`, or `ABANDONED` and optionally confirm its last/death anchor. Omitting an outcome records `ABANDONED`; GitPet never infers `HARD_HANG`.
4. The new generation is `NEWBORN` until its first completed Get, Save, Send, or Reconcile event, then becomes `ACTIVE`.
5. Click the widget body to inspect current and closed generations.

Example for Millenova (the implementation is generic): register generation 8 while Millenova is active, select `MAX_LENGTH` for generation 7, and supply generation 7's last known commit. Generation 7 closes with that death anchor; generation 8 starts at current `HEAD` and displays the connected-node newborn indicator.

## Heuristic score

`ChatVitalityRisk` owns the deterministic 0–100 calculation. Inputs are elapsed time, completed interactions, Gets, Saves/checkpoints, Sends, Reconciles, observed file/churn totals, commits seen, and synchronization cycles. Weights are deliberately isolated for later calibration against longitudinal `MAX_LENGTH`, `HARD_HANG`, and `CLEAN_HANDOFF` evidence.

Classes are `LOW` (0–29), `MODERATE` (30–49), `ELEVATED` (50–69), `HIGH` (70–84), and `HANDOFF RECOMMENDED` (85–100). These are initial product heuristics, not scientifically validated probabilities or model telemetry. `HIGH` recommends handoff after the next clean checkpoint; 85+ recommends creating a clean checkpoint and handing off now.
