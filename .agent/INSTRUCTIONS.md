# EasyLottery agent instructions

## AI development workflow

Before starting repository work, read `.agents/workflow.md`, `.agents/workflow.yaml`, and
`.agents/state/current.md`. Apply the persisted workflow on every new agent session, including
after a Dev Container restart. Keep workflow, decision, plan, verification, and state records in
the repository; do not rely on session-only instructions.

Jev is enabled through TypeSafe for structured workflow-routing decisions. Use it for task
complexity, routing, parallelism, risk, verification level, and approval decisions when the
`TYPESAFE_API_KEY` environment variable is available. Record the typed result in
`.agents/decisions/`; use the documented manual fallback only if the service is unavailable.

When a task needs programmable semantic judgment (for example AI routing, ranking, extraction,
verification, or uncertainty escalation), load the project-local `typesafe-ai` skill before
designing or implementing it. Read its live documentation before adding an API or SDK integration.

## Pull requests

- Create pull requests as **Ready for review** by default.
- Create a Draft pull request only when the user explicitly asks for a draft.
