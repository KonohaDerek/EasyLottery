# Universal Development Checklist

Use this as a short, reusable flow for any project, any language.

## 1. Requirement

- [ ] Confirm the problem, goal, scope, and constraints
- [ ] Ask for missing details
- [ ] Split into separate issues if the work is too broad

## 2. Issue

- [ ] Draft the issue
- [ ] Include background, goal, scope, non-goals, acceptance, and risks
- [ ] Confirm the issue before implementation
- [ ] Do not start coding before the issue is confirmed

## 3. Worktree

- [ ] Check git status, branch, and remote
- [ ] Create an isolated worktree or equivalent branch
- [ ] Keep one issue per worktree
- [ ] Do not use the shared checkout for implementation when a worktree is possible

## 4. Implementation

- [ ] Implement the smallest safe change first
- [ ] Follow the issue or spec step by step
- [ ] Keep unrelated refactors out of scope
- [ ] Re-check the issue if anything is unclear
- [ ] Do not bypass worktree isolation to save time

## 5. Testing

- [ ] Run the most relevant tests first
- [ ] Expand validation if needed
- [ ] Map test results back to acceptance criteria

## 6. PR

- [ ] Write the PR title and description
- [ ] Link back to the issue
- [ ] Summarize what changed, what was tested, and the risks

## 7. Sync

- [ ] Report what changed
- [ ] Report why it changed
- [ ] Report how it was verified
- [ ] Report remaining risks or follow-ups
- [ ] Update issue / PR / docs state if needed
