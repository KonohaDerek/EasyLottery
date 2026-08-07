# YAML 匯入需求

## ADDED Requirements

### Requirement: Import YAML into SQLite
系統 MUST 提供受保護的 YAML 匯入端點，且只有 SQLite provider 可以執行。

#### Scenario: Import existing YAML
- **WHEN** 管理端在 SQLite provider 呼叫匯入端點
- **THEN** 合併 YAML 文件寫入 SQLite 並回傳各資料集合筆數

#### Scenario: Reject import on YAML provider
- **WHEN** provider 為 yaml
- **THEN** API 回傳 409 且不修改 YAML 資料

