# Superpowers installation verification

- Source: `https://github.com/obra/superpowers`
- Installation method: Codex skill installer, using the repository's public `skills/` paths.
- Result: 15 skills installed under `/home/vscode/.codex/skills/`, including `brainstorming`,
  `test-driven-development`, `systematic-debugging`, `using-git-worktrees`,
  `verification-before-completion`, and `using-superpowers`.
- Limitation: the full Codex plugin's automatic session-start hook is available only through the
  Codex Plugin Marketplace, where Superpowers is not exposed in this environment. Skills become
  available from the next Agent session.
- Restart fallback: `.agent/INSTRUCTIONS.md` requires every new session to read the repository
  workflow, so workflow compliance does not depend on the user-level skill installation surviving
  a Dev Container rebuild.
