## Why

目前管理頁與 OBS 頁面依賴人工設定的 `Settings:AdminToken`，未設定或 OBS URL 沒有帶入權杖時會得到 401，且 Donate OBS URL 使用可猜測的數字 ID。直播使用者應能從管理頁直接取得可開啟且受保護的 OBS URL，不必手動維護共用管理密碼。

## What Changes

- 移除 `Settings:AdminToken` 與舊的管理存取權杖流程。
- 新增由 API 啟動時建立簽章金鑰的短期 OBS Session JWT，以及同源的取得端點。
- 讓管理頁自動取得 Session JWT；設定寫入及 OBS URL 均使用該權杖，避免未授權的 401。
- Donate 活動新增不可預測的 GUID 公開識別碼，OBS URL 改以 GUID 定位活動；保留既有數字 URL 的讀取相容性。
- 將戳戳樂、轉盤、加班台與 Donate 的 OBS URL 統一帶入 Session JWT。
- 將舊管理存取頁改為 OBS Session 狀態說明頁。

## Capabilities

### New Capabilities

- `obs-session-access`: 自動發行與驗證 OBS Session JWT，保護管理 API 與 OBS 頁面存取。
- `opaque-obs-activity-urls`: 使用 GUID 作為 Donate OBS 活動 URL 的公開識別碼。

### Modified Capabilities

- 無既有根目錄規格；本次新增完整能力規格。

## Impact

- `src/EasyLotteryAPI`：新增 Session JWT 發行、驗證與端點，並替換各 API 的存取檢查。
- `src/EasyLotteryDomain`、`src/EasyLotteryApplication`、`src/EasyLotteryInfrastructure`：Donate 活動儲存與投影新增 GUID 公開識別碼及舊資料正規化。
- `src/EasyLotteryWasm`：自動取得 Session JWT、產生帶權杖的 OBS URL、更新 OBS 頁與存取說明。
- `tests/EasyLotteryAPITests`：驗證標頭與 query string 的 Session JWT 存取。
- API 新增 `System.IdentityModel.Tokens.Jwt` 套件；瀏覽器重新整理後會建立新的 Session JWT，舊 JWT 在 API 重啟後失效。
