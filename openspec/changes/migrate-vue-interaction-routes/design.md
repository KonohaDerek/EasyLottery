# 設計

## 路由與權限

- Vue 管理 routes 為 /interactions/rounds 與 /interactions/identity-link，沿用現有管理員 session guard，並加入 AdminLayout 導覽。
- Vue OBS routes 使用 GUID roundId，標記為 obs route，不套管理員 shell；共用 App OBS shell 和透明背景。
- API host 只對這六條正式路徑提供 vue/index.html fallback。六個 Blazor pages 改用 /legacy/... aliases，其他未知路徑仍走既有 Blazor fallback。

## REST 與一次性身份碼

- 回合頁沿用 GET/POST /api/interactions/rounds 與 POST /api/interactions/rounds/{id}/{action}，使用既有 X-EasyLottery-Session-Token header。
- 身份綁定沿用 POST /api/interactions/identity-links/complete，body 為 code；不要新增儲存或記錄一次性碼的行為。
- 表單驗證、loading、空狀態和 request errors 由 Vue view 呈現；API/domain contract 不變。

## OBS SignalR

- 每個 OBS route 讀取 roundId 與既有 OBS token，再連線 /hubs/interactions。
- 呼叫 JoinRound(roundId, token)，監聽 InteractionStateChanged snapshot；連線建立、reconnect 與頁面 lifecycle 使用 Vue 既有 @microsoft/signalr dependency。
- 使用自動重連；onreconnected 必須重新 JoinRound，以回到 round group 並取得權威 snapshot。初次連線呼叫失敗、reconnect 尚未恢復、無效 token或 snapshot 為 null 時，提供精簡可讀狀態。
- 元件卸載時解除 handler 並停止 connection。不可把 OBS token 放入 query string、console、telemetry 或錯誤文字。

## OBS 呈現

- 四種 overlay 維持透明背景、可讀文字與 aria-live；依既有功能呈現投票、提示、狀態或排行榜。
- 使用輕量 CSS 呈現初次進場和資料更新，配色/層次符合 EasyLottery 的動畫視覺；不新增 raster artwork 或第三方動畫套件。
- prefers-reduced-motion 時停用非必要位移/縮放動畫，資料和狀態更新仍可讀。

## 驗證、觀測與回復

- Vitest 覆蓋 REST mapping/error handling、SignalR initial join/event/reconnect rejoin/close/error、route metadata。
- Playwright 驗證管理頁 login gate、直接導航/refresh、回合 CRUD transitions、身份碼錯誤、代表性 OBS live/empty/error states 和 Blazor legacy aliases。
- API/Domain interaction tests 確認沒有 backend contract regression；strict OpenSpec 與 diff checks 在 Dev Container 執行。
- 保留 native OBS Browser Source 的解析度、透明合成與實機 reconnect smoke 為使用者驗證項目；Playwright 不冒充 OBS 驗收。
- 回復時可移除 Vue routes/fallback 並把 Blazor route directives 還原；沒有資料庫或資料遷移。
