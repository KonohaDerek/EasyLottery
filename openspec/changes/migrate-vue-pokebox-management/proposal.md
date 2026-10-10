# Vue PokeBox 管理頁遷移

## 背景

Issue #268 追蹤抽獎與活動功能的漸進式 Vue 遷移。Donate 活動、活動結果與 Roulette 模板管理已在 Vue；`/pokebox` 模板管理仍由 Blazor 提供。

## 目標

將 PokeBox 模板管理頁遷移至受管理員工作階段保護的 Vue route，保留既有模板 API、資料格式與管理行為，同時保留 Blazor 回滾入口及未遷移的 preview/OBS routes。

## 範圍

- Vue `/pokebox` 頁面：載入/重試、模板清單、新增、編輯、刪除、複製、啟用狀態、預設模板、匯入/匯出。
- 保留列/欄調整及 PokeCell 欄位、既有 enum 數值與 JSON shape。
- 保留圖片/音訊資產選取、測試 OBS URL、`?edit={id}` 與 `/pokebox/editor/{id}` 入口。
- API host 對精確 `/pokebox` 提供 Vue shell；Blazor 管理頁保留 `/legacy/pokebox`，舊導覽交接到 Vue。
- 加入 Vitest、Playwright 與 OpenSpec 驗證。

## 非目標

- PokeBox 公開操作、preview、OBS browser source、SignalR、抽取/揭露流程遷移。
- 變更 REST API、Domain、資料結構、部署設定、production 或 Blazorise 授權/依賴。

## 驗收

- 未登入者不能讀取 PokeBox 管理 API；管理員沿用既有 session header。
- 管理員可使用現有模板操作、格子編輯與資產選取；載入錯誤與空清單可區分，失敗可重試。
- 編輯取消不污染列表；格數修改按現有行為排序、截斷或新增 cell；內建模板不可由 UI 刪除。
- `?edit={id}` 及 Blazor `/pokebox/editor/{id}` 可開啟同一 Vue 編輯器；`/legacy/pokebox` 保持可用。
- 測試 OBS token 不進入 URL path/query，只放 fragment；E2E 不修改真實模板或建立真實 OBS session。
- Vue tests、typecheck/build、Playwright、.NET build/tests、OpenSpec strict 與 diff 驗證通過。

## 風險、回滾與觀測性

- 主要風險是 PokeTemplate/PokeCell 序列化相容、enum 數值、編輯 query alias、session header 與 fallback 路由順序。
- 不新增 telemetry，也不記錄 session token；API 失敗顯示 accessible alert 並提供重試。
- 回滾時移除精確 `/pokebox` Vue fallback/route/handoff；保留 Blazor 管理頁及現有資料，無需資料回復。
- 不執行 production cutover。
