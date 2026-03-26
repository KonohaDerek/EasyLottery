# OpenSpec Workflow Reference

這份參考文件補充 OpenSpec skill 的執行細節，讓流程可以一致地重複使用。

## Issue 草案格式

```markdown
# <issue title>

## 背景

## 目標

## 範圍

## 驗收標準

## 風險與注意事項

## Checklist
- [ ] 確認需求與限制
- [ ] 建立 worktree
- [ ] 完成開發
- [ ] 完成測試
- [ ] 整理 PR
```

## 執行順序

1. 確認需求。
2. 產生 issue 草案並等待確認。
3. 建立 worktree。
4. 在 worktree 內開發。
5. 執行測試與驗證。
6. 產生 PR 說明。

## 實務準則

- 若需求還不夠明確，先補問句，不直接開始建 worktree。
- 若變更會影響多個區域，先拆 issue，再分別建立 worktree。
- 若是純修正，issue 仍需保留，方便追蹤與驗證。
- PR 內容必須對齊 issue 的驗收標準。
