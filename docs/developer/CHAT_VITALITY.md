# Chat Vitality

Chat Vitality is a ZGitPet-owned, cross-repository engine whose births and histories remain repository-scoped. It estimates **session load / handoff risk** from empirical completed intervals and observable activity. It does not know or claim hidden token counts, context remaining, model limits, or remaining lifespan.

## Persistence and privacy

Data is stored atomically in `%LOCALAPPDATA%\ZomniverseGitPet\chat-vitality.json`, following GitPet's established local JSON-store pattern. Nothing is written into a managed project repository by GitPet itself and the feature makes no network calls. A SHA-256 digest of the normalized repository path is the repository key; the local path is retained only in GitPet's own private state.

Each session records repository identity, generation, chat-first-turn timestamp, first repository contact timestamp, birth commit, optional death-anchor commit, outcome, conversation-turn counters, repository-operation counters, observed incoming changed-file/churn counts, elapsed minutes, and current heuristic score. A birth closes only the previous active interval for the same repository. Commits are anchors in repository history; commits themselves never become "dead."

Completed intervals are retained with repository identity. ZGitPet exposes both per-repository statistics and pooled statistics across all managed repositories. The initial empirical seed is eight completed Millenova chats: `32.3, 58.0, 61.1, 61.4, 17.2, 23.1, 10.6, 45.3` hours (shortest 10.6, longest 61.4, mean 38.6, median 38.8). Those observations remain tagged `wilderruiz/millenova`; they are seed evidence, not universal truth for every repository.

The initial provisional guidance bands are GREEN 0–10h, YELLOW 10–24h, ORANGE 24–40h, RED 40–55h and CRITICAL from 55h onward (55–62h is the currently observed critical range, not a cap). The bands are explicitly provisional and must evolve as cross-repository evidence accumulates. A current snapshot includes chat age, band, repository statistics, pooled statistics, observable activity pressure and provenance. Unobservable pressure candidates such as screenshots or debugging loops are labeled unobserved rather than guessed. Chat death is nondeterministic; no token/context percentage or death deadline is produced.

## Automatic agent birth protocol

Normal agent workflow should not require Wilder to press **+ NEW CHAT**.

A fresh remote coding chat should announce its own birth as its first repository action when Wilder says, for example, `connect to this repo` or `connect to this repo and read shared_knowledge/agents`.

The managed repository may carry one transport envelope at:

```text
.zgitpet/chat-vitality-agent.json
```

Example:

```json
{
  "protocol": "zgitpet-chat-vitality-v1",
  "event": "CHAT_BIRTH",
  "birth_id": "2026-10-15T20:59:00Z-millenova-chat-08",
  "repository": "wilderruiz/millenova",
  "generation": 8,
  "first_turn_at": "2026-10-15T20:59:00Z",
  "birth_commit": "37cbacd94c34d2e28b81816a214dff4dc8982a97",
  "user_turns": 1,
  "assistant_turns": 1,
  "total_turns": 2,
  "previous_outcome": "MAX_LENGTH",
  "previous_death_anchor": "37cbacd94c34d2e28b81816a214dff4dc8982a97",
  "notes": "Fresh coding chat birth announced before repository work."
}
```

Rules:

1. `first_turn_at` is the real conversational birth time. The first `hola` or `connect to this repo` is turn 1. It must not be replaced by first Get, Save, commit, or other Git activity.
2. `birth_commit` is the repository anchor known when the agent first attaches to the repo; it is separate from chat birth time.
3. `birth_id` is mandatory and must be unique for that chat. Re-reading the same envelope is idempotent and must not create another generation.
4. ZGitPet imports the envelope into its private local Chat Vitality store when the repository is refreshed or a tracked repository event occurs.
5. If the same `birth_id` is seen later with higher turn counts, ZGitPet only raises the stored counters; counters never move backwards.
6. The envelope is transport data, not the authoritative long-term database. ZGitPet's local private store remains authoritative for the UI/history.
7. Agents must not create a Git commit for every turn. A birth envelope is mandatory once; occasional later pulse updates may refresh turn totals when a normal repository write/checkpoint already occurs.
8. `+ NEW CHAT` remains a manual recovery/fallback control.

## Cross-repository living-handoff rule

Every agent-originated remote write to repository X must refresh repository X's living next-agent handoff in the same remote write sequence. This applies to code, tools, tests, validators, docs, plans, checkpoints, infrastructure, operational policy and ZGitPet changes. Do not update unrelated repositories' handoffs. If one operation legitimately changes several repositories, refresh the handoff in every affected repository. The handoff must preserve changed scope, checkpoint, evidence, blockers, exact next action, do-not-repeat guidance and safety constraints.

A repository with a shared agent startup document should place this instruction at the beginning of the agent read order: **announce/register this chat birth before doing repository work, then continue the normal handoff read sequence.**

## Repository-operation semantics

GitPet still records **Get, Save, Send and Reconcile** because they are useful session history, but they do not mean the same thing for remote-chat load.

- **Get** is the only GitPet operation treated as a remote-agent interaction. It is the operation that brings work authored by the remote coding chat onto this PC.
- **Save** is a local checkpoint. It does not increase chat load.
- **Send** publishes already-saved local work. It does not increase chat load.
- **Reconcile** is a local repository-history operation. It does not increase chat load.

Only changed-file/churn statistics attached to **Get** are accumulated into the chat-load workload proxy. Local Save/Send/Reconcile churn is deliberately excluded.

## Lifecycle

Agent-announced birth is preferred. Manual **+ NEW CHAT** remains available when no agent envelope exists.

1. Chat begins on its first conversational turn; `StartedAt` / `FirstTurnAt` preserve that time.
2. On first repository contact, ZGitPet records `FirstRepoContactAt` and associates the repository/birth commit.
3. Generation increments independently for that repository.
4. If an active previous generation exists, an explicit `CLEAN_HANDOFF`, `HARD_HANG`, `MAX_LENGTH`, or `ABANDONED` may close it. Missing/unknown outcome is recorded conservatively as `ABANDONED`; GitPet never infers `HARD_HANG`.
5. A new generation remains `NEWBORN` until its first completed **Get** from the remote repository. Local Save, Send or Reconcile operations do not activate it.
6. Click the widget body to inspect current and closed generations.

Example for Millenova: a fresh chat announces generation 8 with the timestamp of the user's first turn and `MAX_LENGTH` for generation 7. Once the envelope arrives locally, ZGitPet closes generation 7 and starts generation 8. The first Get carrying that agent's work moves the generation from `NEWBORN` to `ACTIVE`.

## Heuristic score

`ChatVitalityRisk` owns the deterministic 0–100 calculation. The score is intentionally based on signals plausibly related to the remote coding chat itself:

- elapsed chat age;
- conversation-turn count;
- number of remote Gets;
- number/size of files and line churn observed on those Gets.

Save/checkpoint count, Send count, Reconcile count, local commit count and local synchronization mechanics are retained for history but **do not add to session load**.

Classes are `LOW` (0–29), `MODERATE` (30–49), `ELEVATED` (50–69), `HIGH` (70–84), and `HANDOFF RECOMMENDED` (85–100). These are initial product heuristics, not scientifically validated probabilities or model telemetry. `HIGH` recommends handoff after the next clean checkpoint; 85+ recommends creating a clean checkpoint and handing off now.
