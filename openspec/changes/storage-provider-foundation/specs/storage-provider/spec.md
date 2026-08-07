# 儲存提供者設定需求

## ADDED Requirements

### Requirement: Validate storage provider configuration
系統 MUST 驗證儲存提供者名稱，且 SQLite 設定 MUST 包含連線字串。

#### Scenario: Supported YAML provider
- **WHEN** `Storage:Provider` 設為 `yaml`
- **THEN** 設定驗證成功

#### Scenario: SQLite without connection string
- **WHEN** `Storage:Provider` 設為 `sqlite` 且未設定連線字串
- **THEN** 啟動驗證失敗並指出缺少 `Storage:ConnectionString`

