## ADDED Requirements

### Requirement: 自動取得管理 Session

系統 SHALL 提供同源 `GET /api/session-token`，在不要求管理密碼的情況下核發短期 admin JWT。

#### Scenario: WASM 初次載入

- **WHEN** 瀏覽器 session storage 沒有有效 Session JWT
- **THEN** WASM 呼叫 `/api/session-token` 並將回傳 token 保存於 session storage

#### Scenario: 不再顯示密碼登入

- **WHEN** 使用者開啟網站或重新整理頁面
- **THEN** 系統不得要求輸入管理密碼或讀取 `.admin-password`

### Requirement: 保留 API 權限邊界

系統 SHALL 繼續要求有效 admin JWT 才能呼叫管理 API，並 SHALL 繼續以 scoped OBS JWT 限制 OBS 資源。

#### Scenario: OBS token 呼叫管理 API

- **WHEN** OBS scoped token 呼叫設定、活動 CRUD、Tunnel 或敏感管理 endpoint
- **THEN** API 回傳 forbidden

#### Scenario: API 重啟

- **WHEN** API 執行個體重新啟動
- **THEN** 舊的 admin／OBS JWT 不再被接受，WASM 可重新取得新的 token
