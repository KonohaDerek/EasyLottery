# TypeSafe installation verification

- Source: `typesafe-ai/skills`, skill `typesafe-ai`.
- Method: `npx skills add typesafe-ai/skills --skill typesafe-ai --agent codex --yes`.
- Installation scope: project; installed at `.agents/skills/typesafe-ai`.
- Lock file: `skills-lock.json` records the resolved source and content hash for repeatable
  restoration.
- Tool assessment at installation: Gen Safe, Socket 0 alerts, Snyk Low Risk.
- Usage rule: use for programmable semantic judgments; read live TypeSafe documentation before
  adding an SDK or API integration.
