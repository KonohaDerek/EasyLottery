# 設計

API 使用 `/api/settings/{resource}` 的 GET/PUT 資源路由。每個 handler 只依賴 `ISettingsSectionRepository`，YAML 實作在 `settings.yaml` 的單一檔案鎖內讀寫目標區段；活動與結果檔案不會被設定更新重新序列化。

所有成功讀取與更新回應都回傳 ETag。更新若 `If-Match` 與目前版本不符，回傳 409，讓前端重新載入而不是覆蓋其他瀏覽器的修改。付款資料透過 redactor 遮罩 API key、secret、access token、SMTP 密碼及 YouTube API key。
