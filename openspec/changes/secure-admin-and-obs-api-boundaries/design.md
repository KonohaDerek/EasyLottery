# 設計

## 管理憑證

伺服器以 `Security:AdminPassword` 或 `EASYLOTTERY_ADMIN_PASSWORD` 讀取管理密碼；未設定時在 storage 目錄建立僅限目前使用者讀取的隨機密碼檔，並於首次啟動輸出位置。密碼只用固定時間比較，不寫入 YAML 或送到 OBS URL。

管理頁以密碼交換短效 admin JWT。WASM 將 token 放在 session storage，過期或未登入時顯示登入提示。

## Token scope

JWT 必須帶有 `token_use`：`admin` 或 `obs`。OBS token 另外帶 `resource_kind`、`resource_id`、`scope`，有效期較短。只有 admin token 可簽發 scoped OBS token。

## API 邊界

- Settings、活動 CRUD、Tunnel、支付測試及通知：admin only。
- Live Draw read/commands：符合 kind/publicId 的 OBS token；admin 亦可操作測試頁。
- 支付 callback：維持 provider signature 驗證，不接受 JWT 取代。
- OBS 資源資料以專用 projection endpoint 回傳，不允許讀取完整 settings YAML。

## 防護

登入與敏感命令使用 ASP.NET Core rate limiting；request body 設定上限。拒絕事件以結構化 log 記錄 endpoint、原因與來源，不記錄 token 或密碼。
