# Vue Donate OBS overlay 遷移

## 背景

Phase 4（#269）的 Roulette、PokeBox（含 Live Draw）與 Overtime OBS 頁已遷移至 Vue；Donate OBS 仍由 Blazor WebAssembly 呈現。這個獨立切片對應 #286，隸屬 #269 與 epic #252。

## 目標

- 由 Vue 呈現既有 `/obs/donate/{activityRouteKey}`，不改變 OBS 使用者看到的結果順序、通知、動畫與控制體驗。
- 沿用現有 settings、OBS session token、測試 Donate 與 AI 恭喜文案 API；不新增後端契約。
- 保留 Blazor legacy route，讓切換可回復且不需資料遷移。

## 範圍

- Vue OBS route/view、Donate 結果 queue/reveal lifecycle、動畫場景、媒體失敗回退、透明背景與 reduced-motion。
- 既有 scoped token bootstrap、活動/結果讀取、測試 Donate 與 AI 恭喜文案呼叫。
- 精確 Vue host fallback、Blazor rollback alias、Vitest、focused Playwright、OpenSpec 與驗證紀錄。

## 非目標

- 不變更 Payment、Domain draw rules、API/SignalR contract、production config 或其他 OBS route。
- 不移除 Blazor WebAssembly/Blazorise，不做正式切換；退休仍由 #270 追蹤。
- 不宣稱完成原生 OBS Browser Source smoke，除非實際執行並記錄。

## 驗收標準

- 原 `/obs/donate/{activityRouteKey}` 由 Vue shell 呈現，舊 Blazor 頁可由 `/legacy/obs/donate/{activityRouteKey}` 載入。
- 新 Donate 結果依 ID 去重並按序播放；初始載入不重播舊紀錄，換活動會重設 queue/lifecycle。
- 保留通知、抽獎動畫、webm ready/fail/end、AI 文案、獎品圖片 fallback、最大獎與透明背景。
- 系統偏好及 `motion=reduce` 均能減少動態效果；動畫媒體失敗不會卡住後續結果。
- 測試 Donate 僅呼叫現有 scoped test endpoint，不能送出 payment request；token 不寫入 URL/history/log。
- 專案 Vue tests、typecheck/build、focused Playwright、相關 .NET checks、OpenSpec strict 與 diff checks 通過。

## 測試策略

- Vitest 覆蓋初始化/新結果去重與排序、活動切換、reveal phase、影片完成/失敗與 reduced-motion。
- Playwright 驗證 Vue route、legacy route、透明 overlay、測試 Donate API 與結果顯示；任何會修改測試資料的路徑均需 finally 還原。
- 執行相關 API/domain tests 以確認後端相容性未回歸；不更動其契約。

## 風險、觀測與回復

- 主要風險是 OBS token scope、結果 queue/reveal 競態、動畫媒體事件未完成時阻塞結果。
- UI 顯示可復原的載入/測試錯誤；診斷 log 不得包含 token 或完整敏感設定。
- 若 Vue 驗證失敗，移除 Donate Vue route/fallback 並恢復原 Blazor route；無資料遷移。
- Chromium/Playwright 不等於原生 OBS 驗證；此限制須在 PR 和紀錄中明確標示。
