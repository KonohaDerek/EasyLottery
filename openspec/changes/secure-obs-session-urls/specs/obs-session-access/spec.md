## ADDED Requirements

### Requirement: 自動發行 OBS Session JWT
系統 SHALL 提供同源端點，讓 WASM 取得由目前 API 執行個體簽發、具有到期時間的 OBS Session JWT；系統 MUST 不要求 `Settings:AdminToken` 才能發行或使用此 session。

#### Scenario: 管理頁初次載入
- **WHEN** 管理頁尚未有目前瀏覽器 session 的 token
- **THEN** WASM 取得新的 OBS Session JWT 並用於後續受保護 API 請求

#### Scenario: API 重新啟動
- **WHEN** API 執行個體重新啟動
- **THEN** 先前簽發的 OBS Session JWT MUST 不再被新執行個體接受

### Requirement: 驗證受保護管理與 OBS 請求
設定、支付、加班台及 Donate 活動的受保護 API SHALL 接受 `X-EasyLottery-Session-Token` 標頭中的有效 JWT；OBS 頁面 SHALL 接受 `sessionToken` query string 中的有效 JWT，無效或缺少 token 時 MUST 回應未授權。

#### Scenario: 有效的 API 標頭 token
- **WHEN** 受保護 API 收到有效的 `X-EasyLottery-Session-Token`
- **THEN** API 允許該請求繼續處理

#### Scenario: 有效的 OBS URL token
- **WHEN** OBS 頁收到含有效 `sessionToken` 的 URL
- **THEN** OBS 頁可讀取必要的設定或活動資料

#### Scenario: 缺少或無效 token
- **WHEN** 受保護 API 或 OBS 頁沒有有效 Session JWT
- **THEN** 系統回應 HTTP 401 Unauthorized

### Requirement: 產生可直接開啟的 OBS URL
管理頁 SHALL 在產生 Donate、戳戳樂、轉盤與加班台 OBS URL 時，附上目前有效的 `sessionToken` query string；使用者 MUST 不需手動設定管理存取權杖。

#### Scenario: 使用者開啟 Donate OBS URL
- **WHEN** 使用者在 Donate 活動列表點擊「開啟 OBS URL」
- **THEN** 新視窗 URL 包含可供 OBS 驗證的 `sessionToken`
