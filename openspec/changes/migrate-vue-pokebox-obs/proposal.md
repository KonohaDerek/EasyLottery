# Vue 戳戳樂 OBS 頁遷移

## 背景

戳戳樂管理與預覽 relay 已遷移至 Vue，但正式 OBS browser source `/obs/pokebox/{publicId}` 仍載入 Blazor WebAssembly。舊 overlay 已使用伺服器端 Live Draw 與 SignalR，同一活動可能同時由控制頁與 OBS 顯示，因此遷移不能在瀏覽器自行決定或保存抽取結果。

這是 Phase 4（#269）的一個獨立切片；不代表 Donate、加班台或整個 Phase 4 完成。

## 目標

- 讓既有 PokeBox OBS URL 由 Vue shell 呈現，保留相同的讀取/控制能力與直播視覺。
- 沿用官方 `@microsoft/signalr` client、既有 PokeBox REST/Live Draw API、hub 與 payload。
- 安全處理 OBS session token，並保留 Blazor 回退路由。

## 範圍

- Vue PokeBox OBS route/view、精確 API host fallback 與 Blazor legacy alias。
- 模板與已保存揭露格讀取；Live Draw 初始 snapshot、SignalR 更新、重連加入及最新 snapshot 同步。
- 伺服器控制的手動/隨機戳格、倒數/揭露階段、音效、結果記錄/通知與重設。
- Vitest、Playwright、.NET build/tests、OpenSpec strict 與驗證紀錄。

## 非目標

- 不變更 API、SignalR hub/event/payload、Domain 抽獎規則、持久化格式、支付流程或 OBS 部署設定。
- 不遷移 Donate/Overtime OBS，不移除 Blazor WebAssembly/Blazorise，不執行正式環境切換。

## 受影響範圍

- `src/EasyLotteryWeb`：PokeBox OBS view、route、最小可測的狀態處理與樣式。
- `src/EasyLotteryAPI/Program.cs`：OBS SPA fallback。
- `src/EasyLotteryWasm/Pages/PokeBox/PokeBoxObs.razor`：只增加 legacy rollback route。
- `tests/e2e`、Vue tests、`.agents/state` 與 `.agents/verification`。

## 驗收與風險

- 舊 URL 直接載入 Vue，控制/只讀 session 權限仍由既有 server token scope 驗證。
- 控制端與分離的 OBS source 看到相同的 server-authoritative 狀態；重連後取得 snapshot；token 不留在 URL。
- 保存原有透明 idle、格子/圖片、動畫、音效、結果通知、記錄及重設行為，Blazor rollback alias 可用。
- 主要風險是 token scope 混淆、前端自行抽選、SignalR 重連狀態倒退；以 API contract 測試與雙頁 Playwright 驗證。
- Live Draw phase 目前是記憶體狀態，伺服器重啟後不承諾恢復 countdown/animation；persisted reveal state 仍從模板讀取。此切片不修正後端既有 reset concurrency。
- 回復方式是移除 Vue fallback/route 並恢復原 Blazor URL；不需資料回復。實際 OBS Browser Source smoke 仍需人工驗收。
