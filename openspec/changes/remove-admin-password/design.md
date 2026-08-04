# 設計

## 自動核發管理 Session

保留 `ObsSessionTokenService.IssueAdminToken` 與 `ObsSessionAccess.RequireAdmin`。新增同源 `GET /api/session-token`，不接受密碼、不讀取設定值，直接以 API 執行個體目前的隨機簽章金鑰核發短期 admin JWT，並套用既有 `authentication` rate limit。

管理 API 仍要求 `X-EasyLottery-Session-Token` 標頭；OBS URL 仍使用資源限定的 `sessionToken`。因此移除的是人工密碼登入，不是既有 token 的用途、scope、到期與資源隔離。

## WASM 啟動流程

`configStorage.js` 先從 session storage 或 OBS URL 讀取尚未過期的 token；沒有可用 token 時呼叫 `/api/session-token`，成功後再讀寫設定。流程不再顯示 prompt，也不會讀取 `EASYLOTTERY_ADMIN_PASSWORD`。

## 移除舊憑證來源

移除 `AdminCredentialService`、`/api/admin/session`、`Security:AdminPassword`／`EASYLOTTERY_ADMIN_PASSWORD` 文件與 CI 設定，以及 `.admin-password` 的建立與讀取。既有部署中的 `.admin-password` 不再被使用，可由部署者自行清理。
