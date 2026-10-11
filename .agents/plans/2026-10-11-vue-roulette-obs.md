# Vue Roulette OBS overlay plan

- Source: GitHub issue #269 under epic #252; board status In progress.
- OpenSpec: `migrate-vue-roulette-obs`.
- Scope: Vue `/obs/roulette/{publicId}` overlay, safe OBS token bootstrap, official SignalR client, and Blazor rollback alias.
- Non-goals: PokeBox/Donate/Overtime OBS, API/Domain/hub contract changes, draw-rule changes, production configuration, removing Blazor/Blazorise, closing #269.
- Implementation: main agent owns the coupled UI/session/hub/fallback changes; read-only investigation agent identified the Roulette overlay as the smallest standalone route slice. No parallel writes.
- Verification: 40 Vitest tests; Vue typecheck/build; 3 PokeBox/Roulette Playwright regressions including transient reconnect/rejoin, token scrub, audio-blocked rendering and legacy alias; .NET Release build and 232 tests; OpenSpec strict; diff check; browser viewports at 1920×1080 and 1280×720. Independent read-only review completed with no remaining findings.
- Risk: token handling and reconnect state can break existing OBS sources; preserve exact routes/contracts and retain Blazor fallback. Actual OBS Browser Source smoke and a real server restart/revision-reset are not verified; default automatic-retry exhaustion is covered by unit-tested recovery logic but not E2E.
- Board: Project status remains In progress because the prior Ego TaskSpace 18 is unavailable and `gh` lacks `read:project`; no permission scope was expanded.
- Worktree/branch: `.worktrees/vue-obs-signalr-slice` / `feat/vue-obs-signalr-slice`, based on merged `origin/main` `9eec7afe8a08bc7a8b2a7ab2212d78cbbef6c9ae` (PR #280).
- Tool availability: `TYPESAFE_API_KEY` absent and `superpowers` CLI unavailable; manual structured decision and repository workflow used.
