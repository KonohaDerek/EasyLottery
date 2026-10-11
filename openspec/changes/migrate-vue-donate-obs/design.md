# 設計

## 路由與回退

- Vue 以 `/obs/donate/:activityRouteKey` 載入，不使用管理員 shell；API host 只對精確 OBS path 提供 Vue fallback。
- 原 Blazor component 保留 `/legacy/obs/donate/{ActivityRouteKey}`，原 URL 由 Vue 接管。
- `activityRouteKey` 維持支援既有 public GUID 與正整數 legacy activity ID。

## Token 與現有 API

- 使用共用 OBS token bootstrap：從 fragment 取得 session token 後立刻清理 URL；保留其他 query（包括 `motion=reduce`）。
- 活動設定與 draw records 沿用既有 `/settings` YAML projection 及讀取權限；不以新增 API 或瀏覽器端判斷取代 server authorization。
- 測試 Donate 呼叫現有 `POST /api/obs/donate/{publicId}/test` 並帶 `X-EasyLottery-Session-Token`；AI 恭喜文案呼叫既有 `/api/polaroid-congratulation`，皆不輸出 token。
- 不引入 donation SignalR client：現行 Blazor Donate overlay 以定時 refresh 讀取設定/結果，本切片保持既有資料流。

## 結果與揭露 lifecycle

- 首次 snapshot 只顯示最新一筆，記錄目前活動與已觀察的 record ID，不回放歷史結果。
- 後續 snapshot 只將大於 last-seen ID 的 records 依 ID 排序加入 queue；polling 不得重複加入。
- 活動變更時清空舊 queue 並重設 reveal state；每次只揭露一筆，現有通知、動畫、結果階段和最長動畫時間由同一個 lifecycle 管理。
- 非循環 webm 在動畫 phase 等待 `ended`；影片 ready 失敗、播放失敗或 reduced-motion 時不等待影片，避免 queue 停住。
- unmount 時停止 timer 並中止 fetch；短暫 API 錯誤不得把已排入 queue 的結果丟失。

## 呈現、測試與觀測

- 盡量重用既有 CSS/動畫語意與設定值：animation catalog、polaroid template、獎品/ donor 文案、grand prize、garagara ball color、圖片 fallback 與透明背景。
- `prefers-reduced-motion` 與 `motion=reduce` 都套用；可存取的結果狀態使用 live region。
- Vitest 聚焦純 lifecycle/route 行為；Playwright 使用隔離 fixture 和 finally cleanup，涵蓋 token URL scrub、測試 endpoint、overlay 和 legacy rollback。
- 診斷只記錄非敏感錯誤摘要，不記錄 token、Donor 個資或完整 settings YAML。

## 回復

- 回復時停用 `/obs/donate` Vue route/fallback，使用 `/legacy/obs/donate/{activityRouteKey}`；不涉及資料或 backend rollback。
