# Vue 前端遷移

## 決策

EasyLottery 會以 Vue 3、TypeScript、Vite、Vuetify、Pinia、Vue Router 和 SignalR JavaScript client 替換 Blazor WebAssembly presentation layer。ASP.NET Core .NET 10 API、Domain 與 Infrastructure 保留。

這項決策回應 [Issue #252](https://github.com/KonohaDerek/EasyLottery/issues/252)：不隱藏或繞過 Blazorise 授權提示，改由 Vuetify 承接畫面元件。遷移期間 Blazorise 仍供尚未搬入的頁面使用，因此 #252 要等所有 Blazor 頁面與相依套件移除後才能完成。

## 遷移方式

新版前端暫時掛載於 `/app/`，以便和現有 Blazor 路由並行。`/about`、`/privacy-policy` 與 `/login` 轉向新版頁面；Blazor 導覽選單提供新版入口。未搬遷的活動管理、抽獎及 OBS 頁面仍由原本的 Blazor 應用提供。

Vue build 輸出至 `src/EasyLotteryAPI/wwwroot/app/`，由 ASP.NET Core 同源提供靜態檔與 API。Docker 建置使用 Node.js 24 階段產生前端，再由 .NET 10 階段發布。Passkey client 共用既有 `/api/auth/passkey/*` 與 `/api/admin/passkeys` API，session token 保存在 `sessionStorage` 的 `easy-lottery.session-token` key。

## 本機開發

需要 Node.js 22.18 以上、pnpm 11 與 .NET SDK 10。

```powershell
Set-Location src/EasyLotteryVue
pnpm install
pnpm dev
```

Vite 會在 `http://127.0.0.1:5173/app/` 啟動，並將 `/api`、`/settings` 與 `/hubs` 轉送至 `EASYLOTTERY_API_PROXY` 指定的 API；未指定時使用 `http://localhost:18930`。要由 API 提供 Vue build，執行 `pnpm build` 後啟動 `EasyLotteryAPI`。

## 階段

1. **Foundation**：Vue shell、About、Privacy、Passkey login 與 Passkey 管理頁；建立 API 同源靜態託管與前端測試。
2. **Admin**：YouTube、支付、版面視覺、資產、備份、稽核與管理設定。
3. **Lottery**：首頁抽獎流程、Donate、戳戳樂、轉盤、活動結果與模板市集。
4. **OBS**：各 overlay、動畫與即時 SignalR 流程。
5. **Retirement**：停用 Blazor 路由與 CI，移除 `EasyLotteryWasm` 和所有 Blazorise 套件引用，再完成 Issue #252 驗收。

[Issue #255](https://github.com/KonohaDerek/EasyLottery/issues/255) 的公開頁面、登入信箱預設值、文件語言與隱私文案要求由 Foundation 階段開始處理；其餘首頁驗證與舊頁面標題／標題階層會隨相關功能遷移補齊。
