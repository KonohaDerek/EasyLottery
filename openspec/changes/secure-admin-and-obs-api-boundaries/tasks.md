# 任務

- [x] 建立 admin credential、admin JWT 與 scoped OBS JWT。
- [x] 建立管理登入及 OBS token 發行 API。
- [x] 建立明確 endpoint 權限矩陣與資源 scope 驗證。
- [x] 將 Settings、活動 CRUD、Tunnel 與敏感支付操作限制為 admin。
- [x] 建立單一 OBS 資源 projection，避免讀取完整 settings。
- [x] 移除 client 指定 SMTP credentials 並由 server 設定寄信。
- [x] 加入 rate limit、request body limit 與安全拒絕 log。
- [x] 更新 WASM 管理登入與每資源 OBS URL token。
- [x] 加入權限、過期、scope、rate limit 與 SMTP 測試。
- [x] 撰寫 localhost／Tunnel 操作說明並完成建置驗證。
