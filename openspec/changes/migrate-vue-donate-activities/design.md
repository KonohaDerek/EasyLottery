# 設計

## 路由與授權

- 在 Vue router 增加 `requiresAuth: true` 的 `/donate-activities` route，沿用既有 router guard 與 `apiRequest`。
- API host 只為精確 `/donate-activities` 路徑回傳 Vue shell；Blazor 導覽對該路由 full-page handoff。
- 舊 Blazor component 同時宣告 `/legacy/donate-activities`，供 Vue 問題時回退。
- `apiRequest` 自動附加 `X-EasyLottery-Session-Token`；OBS token 只在點擊測試 OBS 後請求，放在新分頁 URL fragment。

## 資料與編輯器

- TypeScript model 對應 `DonateLotteryActivity` 與 `DonateLotteryPrize` JSON camelCase 欄位；既有 enum 維持整數值，不變更 serialization。
- 新建資料沿用舊頁預設：`PublicId` 隨機 GUID、開始時間現在、結束時間七日後、預設一筆 100% 獎項、結果 15 秒、動畫 8 秒、classic 模板。
- 編輯複製全部 API 欄位，特別保留 activity/prize IDs、PublicId、Type、RemainingQuantity 和所有動畫設定；不讓表單直接改寫列表物件。
- datetime-local 使用本地時間輸入；儲存時轉為 ISO UTC。日期順序及秒數由原生欄位限制與既有伺服器規則共同驗證。
- 純 TypeScript helper 負責安全複製、日期格式化／轉換、機率總和及預設物件，以 Vitest 涵蓋邊界。
- 媒體欄位維持可貼 URL，並新增 accessible native dialog 透過既有 `GET /api/obs-assets` 選擇影片或圖片資產。

## CRUD 與錯誤

- 初始讀取 `GET /api/donate-activities`；保存時依 `id <= 0` 使用 `POST`，否則 `PUT /api/donate-activities/{id}`；刪除用 `DELETE /api/donate-activities/{id}`。
- 只有成功保存／刪除後才重載；保留伺服器回傳錯誤。初始失敗顯示重試，不顯示錯誤空狀態。
- 刪除使用與舊頁等價的原生確認文字，明示既有抽獎紀錄會保留。
- 測試 OBS 先同步開啟空白分頁以避開 popup blocker；取得 session 後設定 `/obs/donate/{publicId}?controls=1#sessionToken=...`。失敗則關閉空白分頁並顯示錯誤。

## 驗證與回滾

- Vitest：預設值、clone 保留欄位、日期 local/UTC roundtrip、機率總和。
- Playwright：未登入不載入 API；管理員讀取、建立、更新、刪除、機率超限、API 失敗重試、資產選取、OBS token fragment 與 legacy 導覽。
- 以 route interception mock 所有 mutation；測試不修改任何真實 API 資料。
- 執行 Vue unit/typecheck/build、必要 .NET build/API regression、Playwright、OpenSpec strict 與 diff check。
- 回滾只移除新 Vue fallback/route/handoff 與 Vue 導覽，保留的 Blazor component 在兩個 path 均可服務，不需資料回復。
