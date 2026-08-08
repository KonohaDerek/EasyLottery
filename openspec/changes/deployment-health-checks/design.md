# Design

使用 ASP.NET Core Health Checks。liveness 不執行外部依賴檢查；readiness 執行 `StorageHealthCheck`，在資料目錄建立短暫 probe 檔後刪除。部署文件描述持久化 volume、備份／還原順序及 session 安全設定。

