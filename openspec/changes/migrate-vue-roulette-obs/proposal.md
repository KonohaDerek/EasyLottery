# Vue 轉盤 OBS 頁遷移

## 背景

轉盤管理頁與測試預覽 relay 已遷移至 Vue，但正式 OBS browser source `/obs/roulette/{publicId}` 仍載入 Blazor WebAssembly。這是 Phase 4（#269）的一個獨立切片；後續 Donate、PokeBox、加班台與其他即時頁仍由 #269 追蹤。

## 目標

- 讓既有轉盤 OBS URL 由 Vue shell 呈現，保留讀取與控制模式。
- 使用官方 `@microsoft/signalr` client 與現有 Live Draw API/hub，維持即時抽獎、倒數、旋轉、結果與記錄行為。
- 將 fragment 中的 OBS session token 安全匯入 `sessionStorage`，並清除網址中的 token。
- 保留 Blazor 回退入口，直到 Vue parity 與 OBS 驗證完成。

## 範圍

- Vue route、API host fallback、轉盤 OBS view 與必要的透明背景樣式。
- SignalR 初始加入、事件接收、自動重連、重新加入群組與重連後 REST snapshot 同步。
- 既有轉盤模板、Live Draw、活動結果與 OBS session REST API 的前端呼叫。
- Playwright 驗證既有 URL、token、讀取/控制、即時旋轉、重連及 Blazor 回退入口。

## 非目標

- 不改 SignalR hub 名稱、事件名稱、payload、server API、Domain 抽獎規則或持久資料。
- 不遷移 Donate、PokeBox、Overtime 或其他 OBS/SignalR 頁面。
- 不改 OBS 部署設定，也不移除 Blazor WebAssembly/Blazorise。
- 此切片不代表 #269 或父 Epic #252 完成。

## 驗收

- 舊 `/obs/roulette/{publicId}` URL 直接載入 Vue，讀取 token 僅使用 read scope，控制頁沿用 `?controls=1` 與 read/control scope。
- Vue 使用既有 `/hubs/live-draw`、`JoinSession("roulette", publicId, token)`、`LiveDrawStateChanged` 與既有 spin/state endpoints；不新增 server contract。
- 重連後重新加入 session 並 GET 最新 state；連線或 session 失敗在控制模式可見且可重新開啟預覽。
- 維持 OBS 透明背景、原有轉盤動畫/結果與鍵盤操作；Blazor rollback alias 可用。
- Vue tests、Playwright、typecheck/build、.NET Release build/tests、OpenSpec strict 與 diff check 通過。

## 風險與回復

- 主要風險是 token fragment 遺漏/外洩，以及重連未重入 hub group 造成控制端更新無法到達 OBS source；以 token 清理測試與雙頁 live-draw E2E 驗證。
- 回復時移除 Vue route/fallback，將既有 Blazor route 恢復為主要 `/obs/roulette/{publicId}`；不涉及資料回復。
- 以 Playwright Chromium 在 1920×1080 與 1280×720 驗證版面；實際 OBS Browser Source smoke test 仍需記錄為人工驗收限制。
