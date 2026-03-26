---
name: OpenSpec Workflow
description: 先確認需求、建立 issue、建立 worktree、開發、測試、再產生 PR 的 issue-first 開發流程；當使用者提到 OpenSpec、gstack、先開 issue、worktree、dev/test/PR 或要求遵循此流程時觸發。
---

# OpenSpec Workflow — issue-first 開發流程

用於所有需要先確認需求，再依序完成 issue、worktree、開發、測試與 PR 的情境。

## 使用時機

- 使用者明確要求遵循 OpenSpec 或 gstack 流程。
- 使用者要求「先開 issue，再建立 worktree」的開發方式。
- 使用者希望將確認、實作與交付拆成可追蹤的步驟。

## 核心原則

- 先確認需求，再建立 issue 草案。
- issue 經使用者確認後，才進入 worktree 與實作。
- 每個 worktree 僅處理單一需求或單一 issue。
- 完成實作後，先測試，再產生 PR。

## 流程

### 1. 確認需求

- 重述目標、範圍、限制、驗收標準與風險。
- 若需求不完整，先提問，不直接猜測。
- 若需求可拆分，先拆成獨立 issue。

### 2. 建立 issue 草案

- 先產生 issue title 與 body。
- issue body 至少包含背景、目標、範圍、驗收、風險與 checklist。
- 在使用者確認前，不進入實作。

### 3. 建立 worktree

- 先檢查 git 狀態、目前分支、既有 worktree 與遠端資訊。
- 依照 [`git-worktree-design`](../git-worktree-design/SKILL.md) 的規則建立 worktree。
- 若該 worktree 需要 spec，於根目錄寫入 `git-worktree-spec.md`。

### 4. 開發

- 依 spec 或 issue checklist 逐項實作。
- 每完成一項就更新進度，必要時同步更新 spec。
- 若遇到模糊處，先回到 issue 確認，不自行補假設。

### 5. 測試

- 先跑與變更最相關的測試。
- 若需要，再補整體驗證或手動驗證步驟。
- 測試結果需能對應到 issue 的驗收標準。

### 6. PR

- 依照 [`git-pr-description`](../git-pr-description/SKILL.md) 產生 PR title 與 description。
- PR 內容需明確連回 issue，並說明變更內容、測試與風險。

## gstack 規範

以下定義用來固定這個 repo 的 gstack 開發節奏：

- G: Issue gate，先確認並建立 issue。
- S: Spec first，先把範圍與驗收寫清楚。
- T: Tree isolation，使用獨立 worktree。
- A: Actual change，只在確認後開始實作。
- C: Check，完成後一定測試。
- K: Keep PR ready，完成後立即整理 PR。

## 參考資料

- [`OpenSpec Workflow Reference`](references/openspec-reference.md)
- [`Issue Template`](references/issue-template.md)
- [`git-worktree-design`](../git-worktree-design/SKILL.md)
- [`exec-worktree-spec`](../../workflows/exec-worktree-spec.md)
- [`git-pr-description`](../git-pr-description/SKILL.md)
