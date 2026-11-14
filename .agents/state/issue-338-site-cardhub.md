# Issue #338 site-side Card Hub state

- Status: Verification complete; independent site/security review passed; awaiting integration review with Hub-base slice 1a.
- Branch/worktree: `codex/issue-338-site-cardhub` / `.worktrees/issue-338-site-cardhub`.
- Base: PR #345 OpenSpec head `6dd9ab2822cfea52f0c3a84165e873033b68d243`.
- Verification: See `.agents/verification/2026-11-14-issue-338-site-cardhub.md`.
- Current state: local changes only; no commit, push, PR, production action or account use.
- Card Hub URL security: `Shop:TrustedHubOrigins` requires exact configured HTTPS origins; loopback HTTP is development-only. Policy is checked before secret use and again in the outbound verifier. Deployment requirements are in `docs/deployment.md`.
- Verification update after URL allowlist and outbound-request contract tests: focused CardShop API 11/11; full API 167/167 from the prior sequential Vue-build run; Vue typecheck, 161/161 tests, production build, OpenSpec strict and diff check passed.
- Remaining integration detail: the linked-state UI shows a generic "linked" status rather than tenant name. Hub 1a now returns `tenant_id` and `tenant_name` at the token endpoint, but 1c does not yet consume them. No acceptance requirement blocks generic status.
