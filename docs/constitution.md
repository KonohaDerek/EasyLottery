# EasyLottery Development Constitution

This document defines the non-negotiable development rules for EasyLottery.
Any repo work that conflicts with this constitution must stop and be corrected before continuing.

## Core mandate

Every change must follow this sequence:

`Requirement -> OpenSpec -> Issue -> Worktree -> Implementation -> Testing -> PR -> Sync`

## Non-negotiable rules

- Do not start implementation before the issue is confirmed.
- Do not skip the worktree step.
- Each issue must have its own isolated worktree.
- One worktree must not be shared across unrelated issues.
- Do not work directly in the main checkout when the task is a feature, fix, or refactor.
- Do not merge or sync incomplete work without verifying the relevant tests.
- Do not "simplify" the process by skipping issue, worktree, or test verification.

## Step rules

### Requirement

- Clarify the problem, goal, scope, and constraints.
- Ask for missing details before implementation.
- Split large or cross-cutting work into separate issues when needed.

### OpenSpec

- Use OpenSpec to structure the requirement before opening the issue.
- Keep the proposal/spec aligned with the eventual issue.
- If the OpenSpec draft is not sufficient, revise it before proceeding.

### Issue

- Convert the requirement into a trackable issue draft.
- Include background, goal, scope, non-goals, acceptance criteria, and risks.
- Do not start implementation before the issue is confirmed.

### Worktree

- Check git status, branch, and remote information first.
- Create an isolated worktree for the issue.
- Keep one issue per worktree.
- If a task cannot be isolated, stop and realign before coding.

### Implementation

- Implement against the issue or spec step by step.
- Prefer the smallest safe change first.
- Keep unrelated refactors out of scope.
- If the design is unclear, stop and re-check the issue.

### Testing

- Run the most relevant tests first.
- Expand to broader validation if needed.
- Map the test result back to the acceptance criteria.

### PR

- Prepare a PR title and description.
- PR descriptions must be complete enough for review without reading the full diff.
- Include a concise summary, what changed, why it changed, how it was verified, and any remaining risk or follow-up.
- Link back to the issue.
- Summarize what changed, what was tested, and the remaining risk.
- If the PR description is too short or vague, treat that as incomplete work.

### Sync

- Report what was changed, why it changed, and how it was verified.
- Update issue, PR, and docs state as needed.

## Enforcement

- If a change would break this constitution, treat that as a blocking issue.
- When in doubt, prefer a slower but isolated workflow over a faster but shared workflow.
