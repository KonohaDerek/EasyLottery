# Vue 加班台 OBS 頁遷移

## 背景

Phase 4（#269）的 Roulette 與 PokeBox OBS 頁已切換至 Vue，但 /obs/overtime 仍由 Blazor WebAssembly 呈現。現有加班台使用 scoped OBS session token、YAML settings projection、overtime feed 與 SignalR 通知；Vue 遷移需保留這些 server-owned contract 與控制權限。

這是 #269 的獨立切片，不代表 Donate OBS、Phase 4 或 Epic #252 完成。

## 目標

- 由 Vue 呈現既有 /obs/overtime URL，保留目前的畫面、控制與即時更新行為。
- 沿用現有 token resource/scope、settings/feed REST endpoint 及 /hubs/overtime contract。
- 保留 Blazor 回退路徑，讓本切片可單獨回復。

## 範圍

- Vue OBS route/view、精確 API host fallback、Blazor legacy alias。
- URL fragment token bootstrap 與清理；讀取 overtime settings projection 和 feed；以既有相容 settings endpoint 保存控制狀態。
- SignalR 初始加入、事件後重新讀取 server state/feed，以及重連後重新加入並同步。
- Countdown、progress、開始/暫停/繼續/結束、測試通知、support event 顯示、主題/動畫與透明背景。
- Vitest、Playwright、相關 .NET tests、OpenSpec strict 與驗證紀錄。

## 非目標

- 不新增或修改後端 API、SignalR hub/event/payload、授權規則、加班計算或 payment/feed generation。
- 不遷移 Donate OBS，不刪除 Blazor WebAssembly/Blazorise，不執行正式環境切換。

## 受影響範圍

- src/EasyLotteryWeb：OBS view、route、settings/feed client 使用方式、最小可測的倒數與即時狀態邏輯。
- src/EasyLotteryAPI/Program.cs：精確 OBS fallback。
- src/EasyLotteryWasm/Pages/Overtime/OvertimeOverlay.razor：保留 legacy rollback alias。
- tests/e2e、Vue tests、.agents/state 與 .agents/verification。

## 驗收與風險

- 原 /obs/overtime URL 由 Vue shell 呈現；legacy alias 仍能載入 Blazor。
- read 與 read/control token 僅用於預期資源和操作；fragment 清理且不記錄 token。
- /hubs/overtime 的 Join(token)、OvertimeStateChanged、OvertimeFeedChanged contract 不變；通知與重連均重新讀取權威設定/feed。
- E2E 在隔離測試環境執行，finally 路徑還原原本 settings/feed。
- /api/settings/overtime 寫入僅允許管理員；OBS control 必須使用既有 scoped /settings 相容路徑。該端點宣告 2026-12-31 sunset，這是保留現行 contract 的後續風險，不在本切片新增替代 API。
- 回復時移除 Vue route/fallback 並使用 legacy Blazor URL；無資料回復。Chromium 驗證不等於原生 OBS smoke。
