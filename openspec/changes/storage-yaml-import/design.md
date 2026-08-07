# Design

管理端呼叫 `POST /api/storage/import-yaml`，由既有 `SettingsFileStore` 讀取合併後 YAML 文件，再透過目前 provider 綁定的 `IEasyLotteryConfigStore` 寫入 SQLite。端點受 admin session 與 sensitive rate limit 保護，並回傳各資料集合筆數。

