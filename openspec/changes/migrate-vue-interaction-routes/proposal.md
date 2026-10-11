# Vue 互動管理與 OBS 路由遷移

## 背景

Phase 4 的互動 API、身份綁定協定與 SignalR hub 已存在，但 Issue #290 所列的兩個互動管理頁與四個 OBS overlay 仍由 Blazor WebAssembly 呈現。Vue 的現有 OBS routes 和 REST/SignalR patterns 可用來完成漸進式切換。

## 目標

- 以 Vue 呈現 Issue #290 六條既有路由，支援直接開啟與重新整理。
- 保留現有 REST、一次性身份碼及 SignalR hub 契約。
- 保留六條 Blazor 頁面的 legacy rollback aliases，直到 parity 驗證完成。

## 範圍

- /interactions/rounds 與 /interactions/identity-link。
- /obs/interaction-vote/{RoundId}、/obs/interaction-notice/{RoundId}、/obs/interaction-status/{RoundId}、/obs/interaction-leaderboard/{RoundId}。
- Vue router、API host fallback、互動前端 client/views、route tests、Playwright。
- 保留 OBS 透明背景、可讀的 loading/disconnected/error states；新增動畫時尊重 reduced motion。

## 非目標

- 不修改 backend、domain、interaction rules、identity-link protocol、SignalR hub/event/payload 或 OBS token scopes。
- 不刪除 Blazor host，不做 production deployment，也不聲稱完成原生 OBS Browser Source 驗證。

## 驗收

- 六個 Vue routes 可直接開啟/重新整理；兩個管理頁走既有管理員 session，四個 OBS 頁走既有 read-scoped session token。
- 回合建立/狀態操作與身份綁定沿用既有 REST request/response contracts。
- OBS client 監聽 InteractionStateChanged，使用 JoinRound(roundId, token)，且 reconnect 後重新加入並更新 snapshot。
- 無效/過期 token、缺少回合、載入失敗及 SignalR disconnect 顯示可理解狀態，token 不寫入 log。
- 六條 Blazor pages 在 /legacy/... 可載入；正式路徑由 Vue shell 呈現。
- Vitest、typecheck/build、互動 .NET tests、focused Playwright 與 strict OpenSpec 驗證通過。

## 風險、回復與觀測

變更只增加 Vue 表現層，不改資料或協定。回復時移除六個 Vue fallback/routes，並將 Blazor pages 還原到原路徑。OBS view 顯示連線/載入狀態；不要記錄一次性碼、session token 或 SignalR query/header 憑證。Playwright/Chromium 結果不等於原生 OBS 驗證，後者由使用者另外確認。
