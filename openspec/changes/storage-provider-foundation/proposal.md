# 儲存提供者切換基礎

## Why

目前儲存實作雖然已抽象成 Repository，但缺少統一的 provider 設定契約，導致後續 SQLite／PostgreSQL adapter 無法安全地加入。

## Goals

- 建立 `Storage:Provider` 與 `Storage:ConnectionString` 設定契約。
- 在應用程式啟動時拒絕未知 provider 或不完整的 SQLite 設定。
- 保持預設 YAML 行為與既有 Repository 不變，讓 adapter 可分階段加入。

## Non-goals

- 本階段不替換既有 YAML Repository。
- 不在本階段執行資料搬移。

