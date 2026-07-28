## 1. OBS Session 存取

- [x] 1.1 建立 API 啟動期簽章金鑰、Session JWT 發行與標頭／query string 驗證服務。
- [x] 1.2 將設定、支付、加班台與 Donate 活動端點改用共用 OBS Session 存取檢查，並提供同源 token 端點。
- [x] 1.3 移除 `Settings:AdminToken` 與舊管理存取流程，改為 WASM 自動取得並在 session storage 保存 token。

## 2. OBS URL 與 Donate 公開識別碼

- [x] 2.1 為 Donate 活動模型、規則、投影與 YAML repository 新增及正規化 `PublicId` GUID。
- [x] 2.2 將 Donate OBS 路由改用 GUID，並保留既有整數 ID 路由的讀取相容性。
- [x] 2.3 更新 Donate、戳戳樂、轉盤與加班台頁面的 OBS URL，使其帶入目前 Session JWT。

## 3. 驗證

- [x] 3.1 新增或更新 API 測試，涵蓋 Session JWT 的標頭與 query string 驗證。
- [x] 3.2 執行 `dotnet test EasyLottery.generated.sln --no-restore` 與 `git diff --check`，確認編譯、測試與格式通過。
