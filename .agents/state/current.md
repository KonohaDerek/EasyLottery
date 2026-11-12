# Current work state

- Task ID: live-site-qa-2026-11-12
- Status: Read-only production-site QA complete; test cases and manual routing evidence recorded locally.
- Source: User request to test `https://easylotter.k-derek.synology.me/` with ego-lite / Playwright and preserve every case.
- Scope: Unauthenticated public routes, protected-route redirect, login-page semantics, three responsive widths, and sampled HTTP security headers.
- Non-goals: No authentication attempt, real draw/payment, data mutation, application implementation, push, PR, merge, or deployment.
- Release route: GitHub latest stable release `v0.0.7` was published (non-draft, non-prerelease) on 2026-10-22 UTC; Claude-plans/Codex-implements policy applies, but no implementation was required.
- Jev: `TYPESAFE_API_KEY` absent; manual structured route was bounded, main-agent, low application-data risk and browser integration verification. See `.agents/decisions/2026-11-12-live-site-qa.json`.
- Branch/worktree: `claude/live-site-e2e-20261112` / `.worktrees/live-site-e2e-20261112`, based on `43f69b802bb12e3c1bda2b4636f892bbc8d775fd`; existing worktree was clean before this task.
- Verification: 9 retained manual cases are in `.agents/verification/2026-11-12-live-site-ego-browser.md`. Findings: About persistence wording filed as #317; HTTP-to-HTTPS behavior is tracked by #253; login semantic heading is tracked by #321. Current branch also contains commit `01aa1d1cfafde98e1c14ca28ba38efd4f636eb2c` with the retained Playwright suite and an evidence report recording 28 passing tests (including 13 expected known-defect failures); this run was not performed by this session. My separate subset rerun was inconclusive (4 passed, 15 failed because Chromium launch was sandbox-blocked and API requests could not resolve DNS); manual ego-browser checks reached the site. No duplicate issues were opened.
- Playwright suite (parallel Claude session): read-only `tests/e2e/live/` suite, `npx playwright test -c live/live.config.mjs` → 28 passed (13 `test.fail` known defects linked to #253, #318–#323). Evidence: `.agents/verification/2026-11-12-live-site-e2e.md`. Competitor-based proposals filed as #324–#333.
- Flags: `verification_complete: true`, `branch_pushed: true`, `pull_request_created: true` (user requested push and PR on 2026-11-12; no merge or deployment).
- Updated: 2026-11-12

## Previous task snapshot

- Task ID: issue-310-participant-unique-removal-2026-10-21
- Status: Implementation and verification complete; Ready PR #311 is open and its initial CI run passed.
- Source: GitHub Issue #310, created from the valid P2 review finding on merged PR #309; #303 is completed and closed.
- Scope: Remove a single lottery participant by its unique participant ID, preserving same-name participants with distinct identities; add a focused mocked Playwright regression.
- Non-goals: No changes to draw behavior, identity deduplication, cooldown rules, participant import/export formats, REST APIs, backend, production, OBS validation, merge, or deployment.
- OpenSpec: Existing `openspec/changes/participant-stable-identity`; this is a small presentation-layer follow-up, so no new OpenSpec change is required. Add one targeted scenario/task to the existing artifacts if it remains consistent with the capability.
- Branch/worktree: `codex/issue-310-participant-removal` / `.worktrees/issue-310-participant-removal`, based on merged `origin/main` commit `8e244d3f399f6edb7bfb2e88ca83db1bf3e8a2a3`.
- Dev Container: `2062a4df92f7`; use `/workspaces/EasyLottery/.worktrees/issue-310-participant-removal` for all edits, builds, and tests.
- Board: Project #1 #310 is In review, Priority P2; #303 is Done; #269 remains In progress pending user-owned OBS validation.
- Routing: TypeSafe Jev 1.13.0 rated the task small (0.98), main-only (0.88), low risk (0.80), browser integration verification (0.98), and no additional approval needed (0.15 probability of yes). See `.agents/decisions/2026-10-21-issue-310.json`. No worker is used.
- Advisor limitation: Workspace Route Advisor is enabled with `executionMode=confirm`, but the current Host's actual model and effort are not exposed. Per workspace instructions, use the repository-native workflow and report this as unverified; do not claim an Advisor Dispatch Plan.
- Verification: Vue tests 130/130; typecheck and production build passed; `openspec validate --all --strict` passed 46/46; `vue-lottery-home.spec.mjs` passed 11/11 using the Development API and mocked provider routes; production dependency audit passed with 0 vulnerabilities; `git diff --check` passed. The regression failed before the code change by removing both entries, then passed after rebuilding the Vue bundle. No .NET unit tests were run because no backend code changed.
- Dependency note: Full npm audit reports 3 advisories in existing development-only packages (`@vitest/mocker`, `tinypool`, `vitest`); the production dependency audit is clean and no dependency files were changed.
- Prior completion: PR #309 merged as `8e244d3f399f6edb7bfb2e88ca83db1bf3e8a2a3`, issue #303 is Done, and final CI run #433 passed. Issue #269 remains In progress for user-owned OBS validation.
- Delivery: Ready-for-review PR [#311](https://github.com/KonohaDerek/EasyLottery/pull/311) links #310; remote PR head is `aecef0ccd043fa9a0ca4fc48eae5cf073868e783`; GitHub Actions CI run #435 passed. Project #1 #310 is In review. Do not merge or deploy.
- Flags: `verification_complete: true`, `branch_pushed: true`, `pull_request_created: true`.
- Updated: 2026-10-21
