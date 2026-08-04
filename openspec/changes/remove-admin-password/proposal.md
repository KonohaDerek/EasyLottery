# 背景

目前網站啟動時仍會要求輸入 EasyLottery 管理密碼，且 API 會讀取 `Security:AdminPassword`、`EASYLOTTERY_ADMIN_PASSWORD` 或建立 `.admin-password`。這與目前以程序記憶體簽章金鑰和短期 Session JWT 保護管理 API 的流程重複，也讓無需帳號登入的本機工具無法直接使用。

# 目標

- 完全移除 EasyLottery 管理密碼、密碼檔與密碼登入 endpoint。
- 由同源 API 自動核發短期 admin Session JWT，WASM 啟動時自動取得。
- 保留 admin token 與 scoped OBS token 的 API 邊界，避免因移除密碼而取消既有資源權限檢查。
- 更新測試、CI、導覽與部署文件，避免再要求設定管理密碼。

# 範圍

- `EasyLotteryAPI` 的 Session endpoint、DI 與管理憑證服務。
- WASM Session storage 啟動流程與存取狀態頁。
- API／E2E 測試、CI 環境變數、README 與安全／發布文件。

# 非目標

- 不導入帳號、角色、外部 Identity Provider 或長期登入憑證。
- 不移除 admin JWT、OBS scoped JWT、JWT 到期或敏感 API rate limit。
- 不變更支付 callback 的 provider signature 驗證。

# 風險與部署注意

未來仍能開啟網站的人即可取得管理 Session，因此公開部署必須使用 localhost、內網或額外的 Tunnel／網路層存取限制。這是移除管理密碼後的預期行為，文件會明確說明。
