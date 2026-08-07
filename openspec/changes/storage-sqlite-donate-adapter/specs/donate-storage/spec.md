# Donate SQLite 儲存需求

## ADDED Requirements

### Requirement: Select Donate repository by provider
系統 MUST 在 `Storage:Provider=sqlite` 時使用 SQLite Donate Repository，`yaml` 時使用既有 YAML Repository。

#### Scenario: Save and list SQLite activity
- **WHEN** SQLite provider 儲存 Donate 活動快照
- **THEN** 重新列出時活動欄位與獎項 payload 保持一致

#### Scenario: YAML compatibility
- **WHEN** provider 為 yaml
- **THEN** 系統維持既有 YAML snapshot 與 event replay 行為

