# SQLite 儲存需求

## ADDED Requirements

### Requirement: Initialize SQLite schema
系統 MUST 在 SQLite provider 啟動時建立 schema migrations、Donate 活動、事件、模板與結果資料表，且重複啟動不得破壞既有資料。

#### Scenario: First SQLite startup
- **WHEN** 使用有效 SQLite 連線字串啟動
- **THEN** 核心資料表會被建立

#### Scenario: Restart with existing database
- **WHEN** 使用相同 SQLite 資料庫再次啟動
- **THEN** migration 可安全重複執行且既有資料保留

