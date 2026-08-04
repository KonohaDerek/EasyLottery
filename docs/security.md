# 管理者與 OBS 權限

## 設定管理密碼

正式部署與 Tunnel 模式建議在啟動前設定：

```bash
export EASYLOTTERY_ADMIN_PASSWORD='請改成足夠長且唯一的密碼'
```

也可使用 ASP.NET Core 設定鍵 `Security:AdminPassword`。管理密碼不會寫入 `settings.yaml`，OBS URL 也不會包含管理 token。

若兩者皆未設定，API 會在 `Storage:Directory` 建立 `.admin-password`。檔案在 Unix 系統只允許目前使用者讀寫；第一次登入時請從主機讀取該檔案。不要把此檔案提交至版本控制或分享給 OBS viewer。

## 管理頁登入

開啟管理頁時，瀏覽器會要求管理密碼並以 `/api/admin/session` 交換四小時 admin JWT。token 只保存在該分頁的 session storage，關閉瀏覽器 session 後需重新登入。

錯誤登入每個來源每分鐘最多五次。設定寫入、活動 CRUD、Tunnel、測試支付與其他敏感命令另有速率限制及 1 MiB request body 上限。

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
- `.admin-password`、storage YAML／JSON、Tunnel credentials 都必須留在可信任主機。
- 公開直播前請以無痕視窗測試正式 OBS URL，確認無法修改設定或操作其他活動。
