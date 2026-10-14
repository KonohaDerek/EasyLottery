# Tasks

## 1. Creator shell and visual system

- [x] 1.1 Add shared high-contrast creator theme tokens and responsive shared-shell surfaces without changing OBS transparency or reduced-motion behavior.
- [x] 1.2 Group existing admin links into keyboard-operable creator workflow disclosures; preserve route targets and active state.
- [x] 1.3 Make logout text/focus visible and retain the existing session-clear and redirect behavior.
- [x] 1.4 Add focused browser coverage for nav, logout, and shared visual tokens at desktop and narrow viewports.

## 2. YouTube setup clarity

- [x] 2.1 Use the dedicated YouTube page as the only editor for system YouTube key and Super Chat settings; replace duplicate fields on Payment Settings with a clear link.
- [x] 2.2 Distinguish the separate interaction-platform credential and explain which key Live Chat uses.
- [x] 2.3 Add a homepage setup/status CTA and concise setup, success, and failure guidance without exposing secrets or adding provider calls.
- [x] 2.4 Test unset/configured/error status and the single-entry path while preserving existing API contracts and masked-secret handling.

## 3. Marketplace preview assets

- [x] 3.1 Add original optimized Roulette and PokeBox artwork with alt text, lazy loading, responsive crop, and a usable fallback.
- [x] 3.2 Test preview semantics/fallback and ensure artwork does not intercept or alter installation actions.

## 4. Donate lifecycle status

- [x] 4.1 Add a deterministic UTC schedule-state helper and test future, exact start, active without end, exact end, invalid dates, and manual-disabled cases.
- [x] 4.2 Show schedule status separately from persisted enablement and verify the list view performs no status-derived writes.

## 5. Verification and delivery

- [x] 5.1 Run frontend Vitest, typecheck, and production build in the project Dev Container.
- [x] 5.2 Run focused Playwright and strict OpenSpec validation; inspect accessibility, secret/data compatibility, reduced-motion/OBS styles, and unrelated diffs.
- [ ] 5.3 Record verification, push the branch, create a Ready PR linked to #252/#294–#299, and move the six issue cards to In review. Do not merge/deploy or perform native OBS validation.
