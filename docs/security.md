# Session 與 OBS 權限

## 自動取得 Session

EasyLottery 網站不需要管理密碼。WASM 啟動時會呼叫同源的 `GET /api/session-token`，由目前 API 執行個體簽發短期 admin JWT，並只保存於瀏覽器的 `sessionStorage`。重新整理或重新開啟瀏覽器後，系統會自動取得新的 token。

預設 `Security:AdminToken:Mode` 為 `local`：只有 loopback（或 TestServer 的本機傳輸）可以取得 admin token。公開部署必須明確切換為 `public` 並設定 `Security:AdminToken:AllowedClientIps`，否則 `/api/session-token` 會回傳 `403`。allowlist 支援單一 IP、CIDR（例如 `192.168.1.0/24`）與 `loopback`。

管理 Token 預設有效期為 60 分鐘，可用 `Security:AdminToken:LifetimeMinutes` 設定（5 分鐘至 24 小時）；OBS Token 預設有效期為 360 分鐘，可用 `Security:ObsToken:LifetimeMinutes` 設定。`DELETE /api/session` 可撤銷目前的 session token；API 重啟後簽章金鑰會更換，所有舊 token 立即失效。

若 API 位於反向代理後方，請將代理的固定 IP 放入 `Security:AdminToken:TrustedProxyIps`。程式只會從受信任代理接受 `X-Forwarded-For`，未列入 allowlist 的直接來源仍無法取得 admin token。

API 重啟會重新產生簽章金鑰，先前的 token 會失效；重新載入網站即可恢復。網站仍應放在可信任的 localhost、內網或已限制存取的 Tunnel 後方，因為沒有帳號登入時，能開啟網站的人也能取得管理 session。

設定寫入、活動 CRUD、Tunnel、測試支付與其他敏感命令仍要求有效的 admin session，並套用速率限制及 1 MiB request body 上限。

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
