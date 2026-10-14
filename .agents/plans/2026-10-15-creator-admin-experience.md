# Creator-first admin experience — #294–#299

## Outcome and boundaries

Improve the Vue creator studio for VTubers and livestreamers: make logout unmistakable, group the admin navigation around live-creator tasks, provide one clear YouTube Live Chat setup path, unify the shared visual language, add original artwork to the template marketplace, and distinguish Donate schedule state from the saved enable flag.

Keep .NET 10, REST/SignalR, Passkey/session policy, YouTube protocol, Domain, payment processing, and credential storage unchanged. Do not run native OBS validation, edit production, merge, or deploy. The user explicitly owns native OBS verification under #269.

## Existing behavior and decisions

- `AdminLayout.vue` already clears the session and redirects to `/login`; the logout control needs visible contrast, focus, and a descriptive accessible name.
- Existing routes remain unchanged. Native `<details>` grouping retains keyboard-operable disclosure behavior without adding a dependency.
- YouTube Live Chat reads `SystemSettings.YouTube.ApiKey`; `/api/interactions/connections` stores a separate `PlatformConnections` value. Payment Settings and the dedicated YouTube page both edit the former. Consolidate that UI to the dedicated page, clearly label the separate interaction value, and never move, clear, log, or expose a secret.
- Donate activity dates are persisted as UTC instants and existing list labels use the viewer's local timezone. Schedule status is derived at render time only; `isEnabled` remains an independent saved setting.
- Marketplace install/copy code remains unchanged. Add two optimized, original, character-free illustrations with meaningful alt text and an accessible fallback.

## Bounded implementation

1. Update Vue theme/global tokens and admin navigation/logout; add focused UI coverage for grouping, active route, keyboard disclosure, and logout visibility.
2. Make `/system/youtube-login` the only UI for the system YouTube key and Super Chat toggle; explain its relation to the separate interaction-platform field; add a contextual CTA and setup/error guidance on the lottery home.
3. Add optimized Roulette/PokeBox marketplace previews with alt text and image-error fallback, preserving install controls and semantics.
4. Add a deterministic UTC-based Donate schedule-status helper and show schedule state alongside the independent enabled/disabled setting. Test start/end boundaries, missing end, and disabled activities.
5. Run focused and complete frontend tests, typecheck/build, focused Playwright and strict OpenSpec validation; inspect the diff, push the branch, open a Ready PR linked to #252 and #294–#299, and synchronize Project #1.

## Acceptance and risks

- Logout is visibly distinguishable on desktop/mobile, keyboard focus is obvious, and the action still clears overtime/admin tokens and routes to `/login`.
- Navigation groups are keyboard operable, active routes remain discoverable, and mobile navigation does not cover the page after selection.
- The main YouTube key is entered in one screen. Other interaction credentials remain in their existing store and are explicitly described as separate; server-side masking and ETag behavior are preserved.
- Public/admin content uses the same accessible color tokens. Reduced-motion and OBS-transparent styling remain unchanged.
- Marketplace cards retain full install behavior when art is unavailable; images are lazy-loaded and each under 150 KB.
- Schedule status is derived with UTC comparisons and displayed with the existing local-time date range. No background or automatic write is introduced.
- Rollback is limited to reverting Vue/CSS/assets; no data migration or API change exists. Observe via browser UI, existing API error states, and CI; no production monitoring change is needed.

## Worktree and workflow

- Branch: `codex/creator-ux-improvements`, based on the latest `origin/main`.
- Work only in `.worktrees/creator-ux-improvements` and use the existing project Dev Container for builds/tests.
- Project #1 begins with #294 in In progress and #295–#299 Ready; all six move to In review only when the PR is open. Keep #252 In progress and #269 In progress; do not change #289 Done.
- PR must be Ready for review. Do not merge or deploy.

## Advisor and delegation

Jev returned `balanced` at confidence `0.99`. The active Host's selected model/effort could not be verified, so the Route Advisor Dispatch Plan could not be validated under `allowModelEscalation=false`; follow the project fallback and do not claim a model profile. Work locally as one integrated validation cycle; do not dispatch a worker without verified model/effort evidence.
