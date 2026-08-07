# Donate SQLite Repository adapter

## Why

SQLite schema 已建立，但 Donate 活動仍固定使用 YAML，`Storage:Provider=sqlite` 尚未真正切換核心 Repository。

## Goals

- SQLite provider 使用 Donate 活動 Repository。
- 以 transaction 儲存活動快照，保留完整 Domain payload 與可查詢索引欄位。
- YAML provider 維持既有行為。

## Non-goals

- 本階段不替換模板與支付 Repository。
- 不移除既有 YAML event-sourcing 實作。

