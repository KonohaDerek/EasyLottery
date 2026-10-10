# Vue Donate activities — Issue #268 slice

需求來源：GitHub #268，前置 PR #275 已合併。

## 範圍

主 Agent 在 `feat/vue-donate-activities-slice` 將 Donate 活動管理 CRUD 從 Blazor 搬至 Vue。沿用既有 activity/assets/OBS-session API，不修改後端或 Domain。Blazor 頁保留 `/legacy/donate-activities` 作回退。

## 非目標與風險

- 不遷移抽獎執行、Donate payment summary、結果通知、OBS source/SignalR、Roulette/PokeBox。
- 不做 production cutover、資料 migration、Blazor/Blazorise 移除或 license 變更。
- 測試中不對 API 發送 CRUD 或 OBS draw mutation；Playwright 攔截所有寫入。
- 風險集中在本地日期轉 UTC、編輯保留獎項庫存、auth header 及 OBS session fragment。

## 驗證順序

1. Vitest 覆蓋 activity default/clone/date/probability helper。
2. Vue unit、typecheck、production build。
3. Playwright 對 authenticated route、完整 CRUD mocks、錯誤／重試／邊界、資產 picker、OBS token 與 Blazor legacy handoff 做 browser 驗證。
4. 在 repo Dev Container 中執行 .NET Release build / API regression、OpenSpec strict、`git diff --check`。
5. 主 Agent 獨立檢視 payload/API 相容性、token 不洩漏、回滾路由與非相關變更，記錄驗證，再 push 和建立 Ready PR，引用 #252/#268。

## 責任與相依

- 主 Agent 負責規格、實作、驗證、整合、push 及 PR；沒有互相獨立且不重疊的第二項工作，不平行派工。
- TypeSafe Jev 不可用時採 repo 規定的手動結構化判斷。PR 建立與 push 僅在所有驗證完成後進行。
