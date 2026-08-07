# SQLite schema 基礎

## Why

Provider 設定已完成，但 SQLite 模式仍沒有啟動時建立的 schema，無法安全地加入後續 Repository adapter。

## Goals

- SQLite provider 啟動時自動建立版本表與核心資料表。
- YAML provider 啟動行為維持不變。

## Non-goals

- 本階段不改變現有 YAML Repository 的讀寫路徑。
- 不宣稱已完成資料匯入或 PostgreSQL adapter。

