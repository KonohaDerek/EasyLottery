## 任務

- [x] 建立四張 GitHub issue：授權、部署安全、外部整合、前端可信度/無障礙修正。
  - #252 https://github.com/KonohaDerek/EasyLottery/issues/252
  - #253 https://github.com/KonohaDerek/EasyLottery/issues/253
  - #254 https://github.com/KonohaDerek/EasyLottery/issues/254
  - #255 https://github.com/KonohaDerek/EasyLottery/issues/255
- [x] 為登入 email 驗證與公開路由版型選擇新增先失敗的測試。
- [x] 實作登入預填、公開版型、首頁驗證時機、metadata、heading 與隱私文字修正。
- [x] 執行相關單元/E2E 測試、`dotnet test EasyLottery.generated.sln` 與建置。
- [x] 以未登入與已登入瀏覽器流程檢查公開頁、首頁與支付頁；記錄限制。
  - 本次程式碼在 Native worktree 的本機暫存儲存區以 Playwright 驗證；正式站尚未部署此分支，故不以正式環境作為變更後驗收來源。
- [x] 更新 issue、OpenSpec tasks 與驗證紀錄；執行獨立 code review。
