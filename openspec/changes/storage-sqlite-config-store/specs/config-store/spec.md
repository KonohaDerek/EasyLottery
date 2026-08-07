# SQLite config store 需求

## ADDED Requirements

### Requirement: Persist domain config in SQLite
系統 MUST 在 SQLite provider 下以 `config_documents` 保存與載入完整 Domain config，並在 YAML provider 下維持既有行為。

#### Scenario: SQLite config round-trip
- **WHEN** Domain service 儲存模板或抽獎結果
- **THEN** 重新載入時資料仍存在且欄位一致

#### Scenario: Provider isolation
- **WHEN** provider 為 yaml
- **THEN** 不會建立或寫入 SQLite config document

