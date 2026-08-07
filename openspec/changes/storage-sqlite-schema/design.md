# Design

`SqliteSchemaMigrator` 實作 `IHostedService`，只在 `Storage:Provider=sqlite` 時開啟連線並執行冪等 `CREATE TABLE IF NOT EXISTS` migration。資料欄位保留 JSON payload 以支援既有 Domain 模型演進，後續 adapter 可逐步將 Repository 對應到欄位與 payload。

