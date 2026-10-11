# 設計

## 路由與呈現

- Vue route `/obs/roulette/:publicId` 不要求管理員登入；每次資料請求與 hub Join 仍須使用該模板範圍的 OBS session token。
- ASP.NET Core 僅為此精確 OBS route 提供 `vue/index.html` fallback。既有 Blazor component 保留 `/legacy/obs/roulette/{PublicId:guid}`，方便回退。
- OBS route 使用獨立全螢幕呈現，不包 `PublicLayout` 或 `AdminLayout`；html/body/app 與 overlay 保持透明，控制區只在 `controls=1` 顯示。

## Session token 與 SignalR

- Vue 啟動時僅於 `/obs/` 路徑讀取 fragment/query 中的 `sessionToken`，寫入既有 session storage key，並以 `history.replaceState` 移除 token；保留 `controls=1` 等非憑證參數。
- 使用官方 `@microsoft/signalr` client 連線 `/hubs/live-draw`，加入時傳入既有 `JoinSession("roulette", publicId, token)`；只處理 `LiveDrawStateChanged` 的既有 JSON 狀態。
- 自動重連期間在控制模式顯示狀態；重連後重新 Join，再以 `GET /api/live-draw/roulette/{publicId}` 取得權威 snapshot。spin 僅 POST 到既有 `/api/live-draw/roulette/{publicId}/spin`，結果記錄沿用既有 activity-results API。
- 不在瀏覽器自行抽獎、計算勝者或改寫 server state；revision 較舊的事件忽略，保留 Blazor 現有的狀態防倒退行為。

## 驗證、觀測與回復

- Vitest 驗證 token 清理、狀態解析/版本排序及連線重入邏輯。
- Playwright 用真實測試 API 與 hub 驗證讀取頁/控制頁同步、鍵盤操作、結果呈現、斷線重連後再操作，以及 legacy alias；不使用真實 OBS 或生產資料。
- 控制模式以 aria-live 狀態顯示 connecting/reconnecting/error/connected；一般 browser source 不顯示診斷文字。
- 回復只需移除新增 Vue fallback/route，並恢復 Blazor route 主路徑；模板與抽獎結果不變。
- Playwright viewport 1920×1080、1280×720 與透明背景檢查可覆蓋一般 Chromium 呈現；不能替代實際 OBS Browser Source 驗收。
