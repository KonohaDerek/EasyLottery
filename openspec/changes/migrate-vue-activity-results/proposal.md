# Vue 活動結果瀏覽遷移

## 背景

Issue #268 要將活動結果與抽獎功能逐步遷移至 Vue。Phase 2 已建立 Vue 管理版型、session token guard 與共用 API client；`/activity-results` 仍由 Blazor 呈現，現有頁面可透過 `GET /api/activity-results` 載入紀錄。

## 目標

將已記錄的活動結果瀏覽遷移到 Vue，保留既有 REST API 與管理員授權方式；維持搜尋、類型與日期篩選、明細展開、摘要統計和 CSV 匯出，並保留 Blazor 版本作為回滾路徑。

## 範圍

- 新增受管理員 session 保護的 Vue `/activity-results` route 與結果檢視頁。
- 使用既有 `GET /api/activity-results` 及 `X-EasyLottery-Session-Token`，不變更後端/API/Domain。
- 提供載入、API 錯誤與重試、空資料、無符合篩選結果等狀態。
- 遷移結果筆數、戳戳樂／轉盤分類、最近活動、搜尋、類型／日期篩選、結果明細及 CSV 匯出。
- 將 API host fallback 與 Blazor 導覽交接限縮到此路由，並加入 Vitest 與 Playwright 驗證。

## 非目標

- Donate 付款統計及 `/api/payments/events`、設定文件的額外讀取。
- Donate 活動 CRUD、Roulette/PokeBox 管理、編輯、抽獎與公開互動流程。
- OBS browser source、SignalR、Domain 規則或 REST API contract 變更。
- 生產環境切換、刪除 Blazor、移除 Blazorise 或調整其授權方案。

## 驗收條件

- 未登入或使用 OBS token 時，Vue route 先導向登入頁且不呼叫活動結果 API。
- 管理員可由 Blazor 導覽進入 Vue；直達 `/activity-results` 也載入 Vue shell。
- 結果依活動日期新至舊呈現；搜尋、類型及本地日期範圍可組合使用，結果明細可鍵盤操作。
- 初次載入失敗時顯示可理解錯誤並允許重試；不得把 API 錯誤呈現成空資料。
- CSV 欄位、UTF-8 BOM、明細排序、引號跳脫與現有匯出相容，且不匯出空篩選集。
- Blazor `/activity-results` 實作仍存在且未遷移的路由不變。
- Vue 單元、typecheck/build、必要 .NET regression、Playwright、OpenSpec strict validation 通過。

## 風險與回滾

- 主要風險為 API 授權 header、Vue/Blazor 路由 fallback、日期時區及 CSV 相容性。
- 若 Vue route 或 host fallback 有問題，可移除本變更新增的 Vue fallback/導覽交接，既有 Blazor 頁仍可用；不需要資料回復。
- 不進行生產切換。部署與 production smoke 由後續核准的操作處理。

## 觀測性

- API 讀取失敗需在頁面以可存取的錯誤區塊呈現，提供明確重試動作；不得記錄 session token 或結果個資到 console。
- Playwright 驗證 route、auth header、失敗重試、篩選、明細及下載行為；本切片不新增後端 telemetry。
