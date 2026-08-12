# EasyLottery 部署手冊

## Production 啟動

正式環境使用 `ASPNETCORE_ENVIRONMENT=Production`，並將 `Storage__Directory` 指向持久化磁碟或 Docker volume。不要把 `App_Data` 放在會隨容器重建而遺失的暫存層。

```bash
docker compose up --build -d
curl --fail http://localhost:18930/health/live
curl --fail http://localhost:18930/health/ready
```

## 備份與還原

部署前先備份 `Storage__Directory`。YAML provider 請一併保留 `*.yaml`、`*.json`、`.bak.*` 與 `config-backups/`；SQLite provider 請備份 SQLite database 檔案。還原時停止 API、還原檔案後再啟動，避免寫入競爭。

## Storage provider

`Storage:Provider` 目前可用 `yaml`（預設）與 `sqlite`。`postgresql` 保留為未來 adapter 的 provider 名稱，尚未註冊 adapter 時會在啟動驗證階段直接拒絕，不會靜默退回 YAML。新增 PostgreSQL 支援時，需同步註冊 Repository adapter 與 migration hosted service。

SQLite 範例：

```bash
Storage__Provider=sqlite
Storage__ConnectionString='Data Source=/data/easylottery.sqlite'
Storage__Directory=/data
```

啟動 SQLite 後，使用管理員 session 呼叫 `POST /api/storage/import-yaml`，將目前 YAML 設定一次性匯入；回應會包含各類資料筆數。備份與回復請保留 SQLite database 檔案，或使用 `/api/settings/backups` API。

## 升級

1. 先執行 readiness probe，確認舊版本健康。
2. 備份資料目錄。
3. 更新 image 或執行檔並重啟。
4. 等待 `/health/ready` 成功，再開放流量。

## Passkey 與網路安全

正式環境使用 Passkey 驗證後簽發短效 JWT。至少設定：

```yaml
Admin__Email: admin@example.com
Admin__Passkey__RpId: easylotter.k-derek.synology.me
Admin__Passkey__Origins__0: https://easylotter.k-derek.synology.me
Security__AdminToken__Mode: public
Security__AdminToken__AllowedClientIps__0: <首次註冊管理員的固定 IP 或 CIDR>
Security__AdminToken__TrustedProxyIps__0: <反向代理固定 IP>
Security__AdminToken__SigningKey: <至少 32 bytes 的 Base64 key>
```

可用 `openssl rand -base64 48` 產生 signing key；固定此值可讓 API 重啟或多副本部署繼續驗證既有 JWT。

首次註冊完成後，請保留 credential 儲存檔 `admin-passkey.json` 與其他資料一併備份；遺失時需要從允許來源重新初始化部署資料。API 不會保存 Passkey 私鑰。
