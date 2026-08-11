# Session 與 OBS 權限

## Passkey 管理登入

EasyLottery 只允許設定的單一管理員 email 登入，預設為 `admin@example.com`。環境變數使用 ASP.NET Core 格式：

```yaml
Admin__Email: admin@example.com
Admin__Passkey__RpId: easylotter.example.com
Admin__Passkey__Origins__0: https://easylotter.example.com
```

第一次登入若沒有 credential，畫面會引導註冊第一個 Passkey。伺服器會真正驗證 WebAuthn registration／assertion，只保存 credential ID、公鑰與 signature counter；Passkey 私鑰只留在裝置或密碼管理器。註冊 challenge 有效期且只能使用一次。

驗證成功後 API 簽發短效 JWT，管理 API 只接受 `X-EasyLottery-Session-Token` header。`GET /api/session-token` 已停用；`DELETE /api/session` 可撤銷目前的 JWT。API 重啟後簽章金鑰會更換，舊 JWT 立即失效。

預設 `Security:AdminToken:Mode` 為 `local`。首次註冊會沿用這個來源政策；公開部署必須設定 `public`、`Security:AdminToken:AllowedClientIps`，並在反向代理環境設定 `Security:AdminToken:TrustedProxyIps`。登入本身由 Passkey 保護，不應把 allowlist 當成唯一登入因素。

WebAuthn 只在 HTTPS secure context（localhost 除外）可用；`Admin__Passkey__RpId` 必須符合網域，`Admin__Passkey__Origins__*` 必須包含瀏覽器實際 origin。設定錯誤時，瀏覽器會拒絕 credential。

設定寫入、活動 CRUD、Tunnel、測試支付與其他敏感命令仍要求有效的 admin JWT，並套用速率限制及 1 MiB request body 上限。

## OBS URL

管理頁為每個 Donate、戳戳樂、轉盤或加班台資源簽發獨立 OBS token。管理列表目前只提供「開啟測試 OBS」入口，使用同一個頁面完成測試與 OBS 擷取：

- 測試 OBS：`read`、`control` scope。
- token 綁定單一 resource kind 與 GUID，不能讀取其他活動或完整設定。
- token 放在 URL fragment（`#sessionToken=...`），fragment 不會送進 HTTP access log；WASM 啟動後會立即移除 fragment 並存入 session storage。

WebM 動畫與 poster URL 建議使用 OBS 資產庫提供的 UUID content URL；影片內容 endpoint 僅提供資產讀取與 range processing，不會把管理 session token 放進影片 URL。

舊的 query-string token 僅保留讀取相容性，請從管理頁重新複製新 URL。OBS 頁面若已帶 scoped token，不會要求取得 admin token；因此公開模式仍可正常使用 OBS 擷取，而不會把管理權限放進 OBS URL。

## Tunnel 注意事項

- Tunnel 啟動、停止與狀態 API 只接受 admin token。
- 支付 callback 不接受 admin／OBS token取代金流 provider signature。
- storage YAML／JSON、Tunnel credentials 與可取得管理 session 的網站都必須留在可信任環境。
- 公開直播前請先在測試 OBS 頁面確認動畫，再用 OBS 瀏覽器來源擷取需要的區域；token 仍只允許目前資源的讀取與控制操作。
