# Vue Roulette 管理頁遷移

## 背景

Issue #268 追蹤抽獎與活動功能的漸進式 Vue 遷移。`/activity-results` 與 `/donate-activities` 已在 Vue；`/roulette` 的模板列表與編輯器仍由 Blazor 提供。

## 目標

將 Roulette 模板管理頁遷移至受管理員工作階段保護的 Vue route，保留既有模板 API、資料格式及管理功能，並保留 Blazor 回滾入口。

## 範圍

- Vue `/roulette` 頁面：載入／重試、列表、新增、編輯、刪除與複製。
- 維持模板設定、6/8/12/24 格編輯、啟用狀態與格子內容的既有 JSON contract。
- 保留載入預設模板、匯入／匯出、OBS 資產選取與測試 OBS 連結。
- API host 對精確 `/roulette` 路徑提供 Vue shell；Blazor 頁保留在 `/legacy/roulette`，導航導向 Vue。
- 加入 Vitest、Playwright 與 OpenSpec 驗證。

## 非目標

- PokeBox、Roulette 編輯器/預覽子路由、公開抽獎、OBS browser source、SignalR 或抽獎演算法遷移。
- 變更 REST API、Domain、資料結構、部署設定、production 或 Blazorise 授權/依賴。

## 驗收

- 未登入或 OBS token 不得讀取 Roulette 管理 API；管理員請求使用既有 session header。
- 管理員可從 Blazor 導覽或直接開啟 `/roulette`，執行現有管理操作；API 錯誤與空清單有明確區別，載入失敗可重試。
- 編輯取消不污染列表；儲存保留 segment 順序、識別碼及設定；內建模板不可由 UI 刪除。
- OBS session token 不進入 query/path，只留在 URL fragment；Vue 測試不建立真實 session 或修改資料。
- `/legacy/roulette` 保持可用，其他 Blazor 路由不變。
- Vitest、typecheck/build、Playwright、OpenSpec strict 與 diff 驗證通過。

## 風險、回滾與觀測性

- 主要風險是 segment JSON 相容性、session header、資產類型篩選及 Vue/Blazor fallback 衝突。
- 不新增 telemetry，也不記錄 session token；API 失敗以頁面 alert 顯示並提供重試。
- 回滾時移除 `/roulette` 的 Vue fallback、route 與 Blazor handoff 白名單項目；保留原 Blazor 頁與後端資料，無需資料回復。
- 不執行 production cutover。
