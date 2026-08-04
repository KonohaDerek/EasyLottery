# Session 與 OBS 權限

## 自動取得 Session

EasyLottery 網站不需要管理密碼。WASM 啟動時會呼叫同源的 `GET /api/session-token`，由目前 API 執行個體簽發短期 admin JWT，並只保存於瀏覽器的 `sessionStorage`。重新整理或重新開啟瀏覽器後，系統會自動取得新的 token。

API 重啟會重新產生簽章金鑰，先前的 token 會失效；重新載入網站即可恢復。網站仍應放在可信任的 localhost、內網或已限制存取的 Tunnel 後方，因為沒有帳號登入時，能開啟網站的人也能取得管理 session。

設定寫入、活動 CRUD、Tunnel、測試支付與其他敏感命令仍要求有效的 admin session，並套用速率限制及 1 MiB request body 上限。

## OBS URL

管理頁為每個 Donate、戳戳樂、轉盤或加班台資源簽發獨立 OBS token：

- 正式 OBS：`read` scope。
- 測試 OBS：`read`、`control` scope。
- token 綁定單一 resource kind 與 GUID，不能讀取其他活動或完整設定。
- token 放在 URL fragment（`#sessionToken=...`），fragment 不會送進 HTTP access log；WASM 啟動後會立即移除 fragment 並存入 session storage。

舊的 query-string token 僅保留讀取相容性，請從管理頁重新複製新 URL。

## Tunnel 注意事項

- Tunnel 啟動、停止與狀態 API 只接受 admin token。
- 支付 callback 不接受 admin／OBS token取代金流 provider signature。
- storage YAML／JSON、Tunnel credentials 與可取得管理 session 的網站都必須留在可信任環境。
- 公開直播前請以無痕視窗測試正式 OBS URL，確認無法修改設定或操作其他活動。
