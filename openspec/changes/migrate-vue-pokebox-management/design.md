# 設計

## 路由與授權

- 新增 Vue admin route `/pokebox`，沿用 `requiresAuth` guard 與 `apiRequest` session header。
- API host 僅對精確 `/pokebox` 回傳 Vue shell；Blazor 管理頁增加 `/legacy/pokebox` alias，NavMenu 以 full-page navigation 交接。
- 管理頁支援 `?edit=0` 新增與 `?edit={id}` 編輯，維持舊 `/pokebox/editor/{id}` Blazor redirect 及 `/pokebox/preview/{id}`、`/obs/pokebox/{publicId}` routes。

## 管理頁與資料

- 使用既有 `/api/poke-templates` GET/POST/PUT/DELETE，以及 duplicate、seed-defaults、import、export endpoints；不建立新 API client 或改動後端。
- TypeScript helper 定義 PokeTemplate/PokeCell 現有 shape、enum 數值、預設值、深層 clone、grid resize 與測試 OBS URL。
- 編輯時深層 clone cells；調整列/欄後按「套用格數」依 Index 排序、截斷或補足，保存既有 cell IDs 與 reveal 欄位。新 cell 沿用現有標題與 `#cccccc` 預設色。
- 重用 `ObsAssetUrlPicker` 的 Image/Audio 類型、共用 API/session client、原生 dialog 和既有管理頁樣式；內建模板不可刪除。

## 狀態、OBS 與錯誤

- `loading`、`error`、`loaded` 區分載入中、失敗與空資料；失敗提供重試，不顯示為空清單。
- 使用者點擊測試 OBS 時才呼叫既有 `/api/obs-sessions`，新分頁使用既有 overlay URL，session token 僅放 URL fragment 並隔離 `window.opener`。
- E2E 攔截模板寫入與 session issuance；不連線到正式資料或真實 OBS session。

## 驗證與回滾

- Vitest 驗證 defaults、deep clone、grid resize/index 保留及 token fragment。
- Playwright 驗證匿名 redirect、不發模板請求、Vue/Blazor handoff、query alias、管理操作、失敗重試、built-in 刪除保護與 legacy rollback route。
- 回滾不需資料轉換：恢復 `/pokebox` 預設交給 Blazor 並保留 Vue code 供後續移除。
