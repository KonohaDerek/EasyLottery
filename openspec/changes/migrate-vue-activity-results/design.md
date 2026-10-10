# 設計

## 路由與授權

- Vue Router 新增 `/activity-results`，設定 `requiresAuth: true` 並使用既有 `adminRouteRedirect`。
- API host 對此精確路徑回傳 Vue `index.html`；其他未遷移路由繼續落到 Blazor。
- Blazor `NavMenu` 對 `activity-results` 使用既有 full-page navigation 交接。
- Vue view 使用共用 `apiRequest` 呼叫 `GET /api/activity-results`。它自動把 session token 放在 `X-EasyLottery-Session-Token`；不把 token 放入 URL。

## 資料與介面

- 只讀取現有 activity-result record；不加入新的 backend abstraction 或 API endpoint。
- 在獨立純 TypeScript helper 中執行搜尋、類型／本地日期篩選、排序與 CSV 序列化，讓行為可由 Vitest 驗證。
- 搜尋範圍與 Domain 現況一致：活動名稱、摘要、類型、結果項目名稱及說明；排序為活動日期降冪，再以 ID 降冪。
- UI 使用現有 Vuetify 與 HTML 原生 `<details>`；標籤、篩選、展開與匯出皆可鍵盤操作。日期依瀏覽器本地時區比較，與 WASM `ToLocalTime()` 行為一致。
- Donate 付款統計需要額外設定與付款事件資料，明確留在後續 slice，不載入舊設定 YAML。

## CSV 匯出

- 欄位維持 `活動 ID,活動類型,活動名稱,活動時間 UTC,摘要,結果順序,獎項,說明,結果時間 UTC`。
- 每個結果項目一列；依項目順序排序；每欄皆以雙引號包覆、內含雙引號以兩個雙引號跳脫；列尾使用 CRLF 並加 UTF-8 BOM。
- 透過瀏覽器 `Blob` / object URL 下載；空結果時停用匯出，不新增 JS interop 或依賴。

## 錯誤與回滾

- `loading`、`error`、`loaded` 為互斥載入狀態；失敗不清空或偽裝成空結果，重試仍只呼叫同一個 GET endpoint。
- 空資料與篩選無結果分開呈現。
- 回滾只需取消 `/activity-results` 的 Vue fallback、Vue route 與 Blazor handoff 白名單變更；原 Blazor頁與 API 不變。

## 驗證

- Vitest：篩選組合、排序、日期邊界、CSV 格式與跳脫。
- Playwright：未授權路由、Blazor 導覽交接、有效管理員 token 的 API header、錯誤重試、篩選／明細及 CSV 下載。
- 執行 Vue unit/typecheck/build、Domain/API regression tests 及 OpenSpec strict validation；Playwright 使用隔離的 Development API storage。
