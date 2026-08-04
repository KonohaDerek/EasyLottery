# 背景

目前 `/api/session-token` 可由任何 client 取得 12 小時 JWT，且同一 token 同時能讀寫設定、管理活動、控制 OBS 與支付測試。結果通知還接受 client 指定 SMTP credentials，Tunnel 操作也沒有權限檢查，因此公開 Tunnel 不具安全邊界。

# 目標

- 將管理者與 OBS token 分離，並驗證用途、scope、資源及有效時間。
- 管理 token 必須以獨立管理密碼登入取得，不再公開自動核發。
- OBS token 只能存取綁定的單一資源及允許操作。
- 設定、活動 CRUD、Tunnel 與測試支付僅允許管理者。
- 郵件 endpoint 只接受通知內容，SMTP 設定由伺服器讀取。
- 對登入與敏感 API 加入 rate/request limits 及拒絕稽核。

# 範圍

- JWT 發行、驗證與權限矩陣。
- 管理登入及 scoped OBS token 發行 endpoints。
- WASM 管理 session 與 OBS URL token 產生。
- Settings、Donate、Live Draw、Overtime、Tunnel、Notification、Payment endpoints。
- 安全測試與操作說明。

# 非目標

- 不引入外部 Identity Provider 或多人帳號管理。
- 不更換既有支付 callback signature 驗證。
- 不改變抽獎演算法。

# 驗收

- 未提供管理密碼無法取得 admin token。
- OBS token 無法讀寫整份設定、活動 CRUD 或 Tunnel。
- OBS token 只能存取其 kind 與 publicId 綁定的資源。
- SMTP host、帳號與密碼不再由 browser request 傳入。
- token 過期、scope 錯誤、權限拒絕及 rate limit 有測試。
- localhost 與公開 Tunnel 的初始管理密碼流程有文件。
