# EasyLottery AI 開發工作流

此檔是本專案 AI 開發工作的長期規範；可執行設定以 `workflow.yaml` 為準。既有
`AGENTS.md`、`.agent/INSTRUCTIONS.md`、OpenSpec 設定及使用者當次明確指示優先。

## 初始化與能力

每次工作先確認平台、可用模型、OpenSpec、測試、Git/worktree 與 CI，並先讀取
`.agents/workflow.yaml`、本檔及 `.agents/state/current.md`。Jev 已透過 TypeSafe 啟用；
每次工作使用它做複雜度、路由、是否可平行、風險、測試層級與人工確認需求的結構化判斷，
並將結果寫入 `.agents/decisions/`。只傳送足以判斷的任務脈絡，不得傳送憑證、secret、
正式環境個資或不必要原始碼。服務無法使用時才改由主 Agent 使用人工結構化決策並記錄原因。
缺少 Superpowers 時，遵守 YAML 內的 `local_incremental_workflow`。Astra 不可用時不得假設
可使用；端到端工作改由 Sol 處理，必要時拆成可安全驗證的小步驟。

Superpowers skills 已從 `obra/superpowers` 安裝，新的 Agent session 可使用其
brainstorming、TDD、debugging、worktree、planning 與 verification skills。若 Dev
Container 重建導致使用者層 skill 消失，仍必須依本專案 `local_incremental_workflow`
繼續工作，不得因此中止；可依本檔記錄的來源重新安裝。

TypeSafe 已安裝為專案層 skill（`.agents/skills/typesafe-ai`），會隨專案保留。當功能
需要可程式化的語意判斷，例如路由、排序、結構化抽取、證據驗證或不確定性升級時，先
載入 TypeSafe skill 並讀取當前官方文件；確定性規則、計算與副作用維持在應用程式程式碼
中，且 API 憑證只可保存在伺服器端。

## 任務路由

- 簡單、低風險、單一可快速驗證的改動由主 Agent 處理；可使用 Luna。
- 一般功能、分析與 bug 預設 Terra。
- 跨模組、資料庫、Saga、MQ、Redis、分散式流程與測試設計使用 Sol。
- 僅當至少兩項任務互相獨立、檔案不重疊且各自在獨立 worktree/branch 時，才可平行
  使用子 Agent；子 Agent 預設 Sol，不得自行合併。
- 任務不明確時，主 Agent 使用 Terra 且不啟用子 Agent。

## 標準流程

1. 記錄需求、情境、非目標、假設、風險及回滾／觀測性需求。
2. 中大型變更建立 OpenSpec change，包含 proposal、design、tasks、可驗收條件、測試、
   回滾與觀測性內容。
3. 設計測試策略並實作可驗證的小步驟；測試失敗需先分析。
4. 執行相關單元、整合、契約、E2E、回歸或效能驗證，並將命令、通過／失敗數、覆蓋
   缺口與環境限制寫入 `.agents/verification/`。
5. 由主 Agent 獨立核對需求、驗收、邊界、相容性、migration/rollback/monitoring、非
   相關變更，以及安全、效能、併發風險，輸出結構化驗證結果。
6. 更新 OpenSpec 與交付紀錄。高風險變更不可自動合併，需使用者確認。

## 工作回報

開始時回報：工作、需求來源、OpenSpec change、主／子 Agent 模型、任務與相依、測試
層級、風險、人工確認需求。完成時回報：完成內容、修改檔案、OpenSpec 與子 Agent
結果、測試與驗證結果、未解決問題、剩餘風險與下一步。

## 流程變更

使用者以自然語言變更流程時，先說明影響，然後同步更新本檔與 `workflow.yaml`；新近
且明確的使用者指示優先於衝突規則。

此流程位於專案版本控制範圍，並由 `.agent/INSTRUCTIONS.md` 作為 Agent 入口；因此
Dev Container 重啟或建立新 Agent session 後，都會重新載入相同規則。
