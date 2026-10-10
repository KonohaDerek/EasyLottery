# 設計

## 路由與授權

- 新增 Vue admin route `/roulette`，沿用既有 `requiresAuth` guard 與 `apiRequest` session header。
- API host 僅對精確 `/roulette` 回傳 Vue `index.html`；Blazor `/roulette` 頁增加 `/legacy/roulette` alias，NavMenu 以 full-page navigation 交接。
- Vue layout 增加 Roulette 導覽項目；編輯器、preview 與 OBS 子路徑仍留在 Blazor。

## 管理頁與資料

- 使用既有 `/api/roulette-templates` GET/POST/PUT/DELETE，及既有 duplicate、seed-defaults、import、export endpoints；不建立 API client 或改動後端。
- 以獨立 TypeScript helper 定義既有 JSON shape、預設模板/segments、深層 clone、改變格數及測試 OBS URL，讓非 UI 規則可由 Vitest 驗證。
- 編輯時 deep clone 模板與 segments；儲存時依 `Index` 排序並保留既有 segment IDs。格數沿用 6、8、12、24；新增格使用原 Blazor editor 的預設標題與色票。
- 使用原生可捲動 `<dialog>`、既有表格/表單樣式及 `ObsAssetUrlPicker`；擴充 picker 僅為支援已存在的 Audio 資產類型。
- 匯入/匯出維持原本 JSON endpoint；built-in 模板不顯示刪除動作。

## 狀態、OBS 與錯誤

- `loading`、`error`、`loaded` 區分載入中、失敗與空資料；所有失敗顯示 alert，不把失敗當成空列表。
- 測試 OBS 按鈕在使用者點擊時呼叫既有 `/api/obs-sessions`；開啟的既有 OBS URL 將 token 放在 fragment，並清除 popup opener。
- API 寫入與 session issuance 在 Playwright 中全部攔截；不連線至真實活動資料。

## 驗證與回滾

- Vitest 驗證 clone 隔離、格數縮放/補足、segment 順序與 OBS token URL。
- Playwright 驗證 unauthenticated redirect、Blazor handoff、管理 CRUD/錯誤重試、內建刪除保護及 `/legacy/roulette`。
- 回滾不需要資料遷移：恢復 `/roulette` 至 Blazor fallback 並保留 Vue code 可在後續移除。
