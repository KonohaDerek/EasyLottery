# SQLite Domain config store

## Why

模板與抽獎結果服務透過 `IEasyLotteryConfigStore` 讀寫完整設定文件；若只切換 Donate Repository，模板與結果仍會寫入 YAML。

## Goals

- SQLite provider 使用 JSON payload config store，讓既有 Domain services 可持久化於 SQLite。
- YAML provider 維持原有分拆 YAML 與備份功能。

## Non-goals

- 管理介面的 YAML backup API 不在本階段改造。

