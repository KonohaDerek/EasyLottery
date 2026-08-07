# Design

Infrastructure 以 `StorageProviderOptions` 集中描述 provider 名稱與連線字串，透過 Options binding 與 `ValidateOnStart` 在啟動時驗證。支援名稱先固定為 `yaml`、`sqlite`、`postgresql`，SQLite 要求連線字串；實際 adapter 會在後續變更中以相同設定切換 DI。

