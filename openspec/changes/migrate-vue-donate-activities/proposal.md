# Vue 捐款活動管理遷移

## 背景

Issue #268 要逐步將活動管理與抽獎體驗從 Blazor 移至 Vue。Vue Phase 2 已提供 admin session guard 與共用 API client；Activity Results 已遷移，但 `/donate-activities` 仍由 Blazor 管理活動。

## 目標

將捐款活動管理頁搬到 Vue，保留既有 REST API、管理員授權、活動欄位、獎項規則、資產選擇與測試 OBS 開啟方式。Blazor 頁保留在明確的 legacy 路徑以便回退。

## 範圍

- Vue 新增受管理員保護的 `/donate-activities` 活動清單與 CRUD 編輯器。
- 沿用 `/api/donate-activities`、`/api/obs-assets` 與 `/api/obs-sessions`；不修改 API 或 Domain。
- 支援活動動畫／結果顯示設定、WebM 欄位、日期、啟用狀態、獎項 CRUD 與機率總和檢查。
- 保留 OBS 本機媒體資產選擇器、刪除確認及開啟測試 OBS（token 放 URL fragment）。
- 為 Vue route、API 錯誤／重試、表單邊界及 Playwright 寫入流程加入測試；E2E 寫入只攔截在瀏覽器 mock，不碰測試 API 的活動資料。
- API host 精確 fallback 只增加 `/donate-activities`；Blazor 舊頁另保留 `/legacy/donate-activities`。

## 非目標

- Donate 付款統計、抽獎執行、結果通知、OBS browser source、SignalR、Roulette/PokeBox。
- Domain、資料儲存、REST API contract、Payment、YouTube、SMTP、WebAuthn/Passkey 的修改。
- Production cutover、刪除 Blazor/Blazorise 或任何 Blazorise license 操作。

## 驗收條件

- 未登入或使用 OBS token 時，Vue 頁導向管理員登入，且不呼叫捐款活動 API。
- 管理員可由 Blazor 導覽進 Vue；直達 `/donate-activities` 顯示 Vue；legacy URL 仍提供 Blazor 頁。
- 活動列表可新增、編輯、刪除；request 使用現有 token header 與 API 方法／payload。
- 編輯保留既有 ID、PublicId、獎項 ID 與剩餘庫存；日期以瀏覽器本地時間編輯並以 UTC 儲存。
- WebM、結果顯示秒數、動畫秒數、模板、AI/Donate 顯示旗標、獎項媒體／數量／機率／最大獎等現有欄位均可操作。
- 機率總和大於 100% 不可儲存；API 錯誤清楚呈現且不偽裝成空列表。
- 測試 OBS token 由既有 API 取得，僅在使用者啟動時建立，且放入 `#sessionToken` fragment，不放 query string。
- Vitest、Vue typecheck/build、authenticated Playwright 與 OpenSpec strict validation 通過。

## 風險、回滾與觀測性

- 風險：含 UTC offset 的日期往返、更新時保留獎項庫存、Vue/Blazor 精確 fallback、session token 洩漏。
- 回滾：將導覽交接或 `/donate-activities` 的 Vue fallback 恢復為 Blazor；`/legacy/donate-activities` 在此期間維持可用。沒有資料轉換或 migration。
- UI 需分別呈現載入、空資料、錯誤、保存中與刪除中狀態。避免將 token 寫入 console、query string 或 storage。
- E2E 的新增、更新、刪除與 OBS session 發行均使用 Playwright route mocks；不呼叫 API 寫入真實活動資料。
