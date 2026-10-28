# ZGit Pet Post-Check `index.lock` Race Hardening Plan

**Status:** IMPLEMENTATION ACTIVE  
**Observed:** 2026-10-28 on Millenova during **Get ↓**  

## Problem reproduced

GitPet correctly performed the existing pre-mutation `index.lock` check, then `git pull --ff-only` still failed with:

```text
Unable to create '.../.git/index.lock': File exists.
```

The repository remained clean and the Get operation was safely aborted. Manual inspection showed the leftover lock was an empty stale file with no active Git writer holding it. Removing only `.git/index.lock` allowed Get to fast-forward normally.

The existing Oct 24–25 hardening protects the state *before* `add` / `pull`, but there is still a narrow time-of-check/time-of-use gap: an `index.lock` can appear after the pre-check and before or during the Git mutation.

## Safety contract

GitPet must never delete an `index.lock` merely because Git printed a lock error.

For guarded mutations (`git add` and `git pull`):

1. keep the existing pre-mutation lock recovery;
2. run the Git command once;
3. only if that command fails with the specific Git `index.lock` collision signature, inspect the exact repository lock again;
4. if the exact lock can be exclusively opened/deleted using the existing stale-lock proof, remove it and retry the same Git command **once**;
5. if the lock is held/in-use, stop and return the existing safe GitPet message;
6. if the lock vanished between failure and inspection, retry once because the competing operation has completed;
7. never loop, never force, never delete another path, and never weaken `pull --ff-only`.

## Regression coverage

Add deterministic coverage for:

- identifying an `index.lock` collision without misclassifying unrelated Git failures;
- first mutation fails with stale lock → lock recovery succeeds → exactly one retry succeeds;
- first mutation fails with lock collision → lock is actively held → no retry and no deletion;
- collision disappears before recovery → one retry is allowed;
- retry itself fails → failure is returned without a third attempt.

## Exit gate

A stale `index.lock` appearing in the gap between GitPet's pre-check and `git pull` / `git add` self-heals once, while a genuinely active Git writer remains protected and blocks the mutation.