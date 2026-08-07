# Design

`SqliteDonateActivityRepository` 實作既有 `IDonateLotteryActivityRepository`，活動快照以 JSON payload 保存，並同步寫入 public id、名稱、日期及啟用狀態欄位。DI 依 `StorageProviderOptions.NormalizedProvider` 選擇 SQLite 或 YAML 實作；寫入使用 transaction 避免半套快照。

