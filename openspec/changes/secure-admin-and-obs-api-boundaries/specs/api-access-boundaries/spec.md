# API Access Boundaries

## ADDED Requirements

### Requirement: Explicit administrator authentication

系統 SHALL 只在有效管理密碼驗證後核發短效 admin token。

#### Scenario: Anonymous token request

- **WHEN** 未驗證 client 要求管理 token
- **THEN** 系統拒絕要求且不回傳 token

#### Scenario: Valid administrator login

- **WHEN** 使用者提供正確管理密碼
- **THEN** 系統核發帶有 admin token use 的短效 JWT

### Requirement: Resource-scoped OBS access

OBS token SHALL 綁定單一 resource kind、publicId 及允許 scope。

#### Scenario: Access another activity

- **WHEN** Donate A 的 OBS token 要求 Donate B 的資源
- **THEN** 系統回傳 forbidden

#### Scenario: Modify settings with OBS token

- **WHEN** OBS token 嘗試寫入 settings 或管理活動
- **THEN** 系統回傳 forbidden

### Requirement: Protected sensitive operations

設定、活動 CRUD、Tunnel、測試支付與寄信 SHALL 僅允許 admin token。

#### Scenario: Start tunnel anonymously

- **WHEN** 未驗證 client 要求啟動 Tunnel
- **THEN** 系統拒絕操作

### Requirement: Server-owned mail configuration

結果通知 endpoint SHALL 從伺服器設定讀取 SMTP 連線資料，不接受 client 指定 host 或 credentials。

#### Scenario: Send result notification

- **WHEN** admin client 提交通知收件者與內容
- **THEN** server 使用已儲存 MailDelivery 設定寄送

### Requirement: Abuse protection

登入與敏感 endpoints SHALL 套用 rate limit 與 request size limit。

#### Scenario: Exceed login attempts

- **WHEN** 同一來源超過允許的登入速率
- **THEN** 系統回傳 HTTP 429
