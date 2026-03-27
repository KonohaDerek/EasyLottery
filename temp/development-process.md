# Universal Development Process

This file is a language-agnostic development SOP that can be reused across different projects and stacks.
It follows the same workflow constitution pattern used in EasyLottery:
Requirement -> OpenSpec (or equivalent planning) -> Issue -> Worktree -> Implementation -> Testing -> PR -> Sync.

If you want a shorter version, use [Universal Development Checklist](./development-process-checklist.md).

## Core flow

1. Requirement
2. Issue
3. Worktree
4. Implementation
5. Testing
6. PR
7. Sync

## Step details

### 1. Requirement

- Clarify the problem, goal, scope, and constraints.
- Ask for missing details before implementation.
- Split large or cross-cutting work into separate issues when needed.

### 2. Issue

- Convert the requirement into a trackable issue draft.
- Include:
  - Background
  - Goal
  - Scope
  - Non-goals
  - Acceptance criteria
  - Risks and notes
- Do not start implementation before the issue is confirmed.
- Do not skip the issue step.

### 3. Worktree

- Check git status, branch, and remote information.
- Create an isolated worktree or equivalent branch isolation.
- Keep one issue per worktree.
- Do not work in a shared checkout when isolation is possible.

### 4. Implementation

- Implement against the issue or spec step by step.
- Prefer the smallest safe change first.
- Keep unrelated refactors out of scope.
- If the design is unclear, stop and re-check the issue.
- Do not bypass the worktree or issue steps to move faster.

### 5. Testing

- Run the most relevant tests first.
- Expand to broader validation if needed.
- Map the test result back to acceptance criteria.

### 6. PR

- Prepare a PR title and description.
- PR descriptions must be complete enough for review without opening the full diff first.
- Include a concise summary, what changed, why it changed, how it was verified, and any remaining risk or follow-up.
- Link back to the issue.
- Summarize what changed, what was tested, and the remaining risk.
- If the PR description is too short or vague, treat it as unfinished work.

### 7. Sync

- Report completion clearly.
- Include:
  - What was changed
  - Why it changed
  - How it was verified
  - What remains risky or incomplete
- Update issue/PR/docs state as needed.

## Reusable rules

- Keep changes scoped.
- Make outcomes verifiable.
- Prefer explicit acceptance criteria over vague goals.
- Preserve existing conventions unless the issue says otherwise.
- Treat worktree isolation and issue confirmation as required defaults, not optional optimizations.
