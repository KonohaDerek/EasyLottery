# 部署健康檢查需求

## ADDED Requirements

### Requirement: Expose liveness and readiness endpoints
系統 MUST 提供 liveness 與 readiness health endpoint；readiness MUST 檢查資料目錄可讀寫。

#### Scenario: Healthy deployment
- **WHEN** API 正常啟動且資料目錄可寫
- **THEN** `/health/live` 與 `/health/ready` 回傳成功

#### Scenario: Storage unavailable
- **WHEN** 資料目錄無法建立或寫入
- **THEN** `/health/ready` 回傳失敗狀態

