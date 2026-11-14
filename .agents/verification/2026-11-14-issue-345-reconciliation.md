# Card Shop OpenSpec review reconciliation — 2026-11-14

- Base revision: `6dd9ab2822cfea52f0c3a84165e873033b68d243` (PR #345 head).
- Worktree: `.worktrees/issue-345-spec-reconciliation`; branch `codex/issue-345-spec-reconciliation`.
- Scope: reconcile review findings in the Card Shop design, capability specs and task list. No application code, GitHub PR metadata, account, or production configuration changed.

## Decisions recorded

- A published finite-supply pack has immutable odds. Stop sales when any rarity is depleted; publish a new odds version before resuming.
- A 13–17 viewer may register, collect and redeem gifted cards, but cannot enter a paid draw in Wave 1. Do not claim guardian consent.
- Resend the same still-valid redemption code to the original consented address without extending expiry. Keep address and any recoverable code copy only in encrypted delivery data and delete all delivery copies no later than 180 days after the later of redemption or expiry.
- Specify tenant deletion notices and viewer export boundaries.
- Separate the platform-admin credential from viewer/site credentials, require fail-closed authorization, HTTPS outside loopback development, rate limits and audit events.
- Defer native iOS store and capture acceptance; preserve shared React Native portability and iOS Safari web flows.
- Defer native iOS wallpaper export as well; Android wallpaper export is the phase-one acceptance target.

## Commands and results

| Check | Command | Result |
| --- | --- | --- |
| Strict OpenSpec | `npx --yes @fission-ai/openspec@1.7.0 validate streamer-card-shop --strict` | Passed: change is valid. Executed on host because no Dev Container exists for this new worktree; this Node CLI check does not install or invoke .NET. |
| Whitespace | `git diff --check` | Passed. |
| Decision JSON | `python3 -m json.tool .agents/decisions/2026-11-14-issue-345-spec-reconciliation.json` | Passed. |

## Remaining gates

- The PR #345 body still contains stale owner questions; editing it requires the owner's GitHub account authorization.
- The paid-callback/last-instance recovery procedure, including any refund behavior, remains unresolved and blocks production payment handling. This can incur a monetary transaction and needs human owner approval.
- Age declaration, withdrawal-right copy, privacy notices, and service-notice consent need legal review before production use.
- Hub hosting, domain, email provider and legal entity remain operations decisions.
- No same-SHA CI or formal review evidence has been added to PR #345; do not merge based on the current evidence.
