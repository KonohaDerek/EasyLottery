# 部署健康檢查與文件

## Why

部署範例與環境設定容易漂移，缺乏可供容器編排使用的 liveness／readiness 判斷。

## Goals

- 提供 `/health/live` 與 `/health/ready`。
- readiness 驗證資料目錄可讀寫。
- Docker Compose 使用 Production 並補充備份、還原、升級手冊。

