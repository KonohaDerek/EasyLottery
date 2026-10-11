# Tasks

- [x] Add tested Vue overtime API, YAML projection, feed, time/session, and stale-snapshot helpers.
- [x] Implement the Vue `/obs/overtime` overlay with existing scoped REST, feed, and SignalR contracts.
- [x] Preserve transparent rendering, countdown/progress, manual controls/markers, support messages, completion effects, reconnect/error states, `motion=reduce`, and one-second display updates; unit-test bounded coalesced refresh and reconnect re-join/refresh/failure handling.
- [x] Route exact `/obs/overtime` requests to Vue and keep `/legacy/obs/overtime` on the original Blazor component.
- [x] Extend the isolated Playwright flow for read/control scopes, start/pause/resume/end, connected-client settings polling, expired feed-marker completion/retry, stale feed-marker precedence, test amount consistency, live hub/feed notifications, URL scrubbing, reduced motion, transparent rendering, legacy fallback, and exact settings/feed restoration.
- [x] Run and record Vue unit tests, typecheck/build, focused overtime Playwright, relevant .NET Release tests, strict OpenSpec validation, and diff checks in the project Dev Container.
- [ ] Complete an isolated whole-branch review; resolve material findings and rerun verification.
- [ ] Push the feature branch, create a Ready-for-review PR linked to #284/#269/#252, and move #284 to Needs Review in Project #1. Do not merge or deploy.
