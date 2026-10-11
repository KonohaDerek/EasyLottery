# PokeBox OBS Vue migration plan

- Request source: merged PR #281 and Phase 4 issue #269 under epic #252; implementation slice tracked by #282.
- Scope: replace only `/obs/pokebox/{publicId}` presentation with Vue; preserve REST/SignalR/token contracts and legacy Blazor rollback.
- Non-goals: backend/domain/contract changes, Donate/Overtime OBS, production cutover, deleting Blazor.
- Main agent implements and verifies. Read-only contract investigator Goodall (`gpt-6-luna`, high) completed an independent API/behavior inventory; no files changed by that agent.
- Jev/TypeSafe could not run because `TYPESAFE_API_KEY` is absent. Superpowers executable is unavailable in the host environment; use the repository's documented local incremental workflow and Ponytail.
- Risk: OBS session-token handling and server-authoritative real-time state. Validation: focused Vitest, Playwright with separate control/OBS clients, .NET Release build/tests, OpenSpec strict, diff review.
- Rollback: retain `/legacy/obs/pokebox/{publicId}` and restore the prior host route; no data migration.
