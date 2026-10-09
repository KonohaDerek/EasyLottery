# Vue 3 前端漸進遷移

## 背景

EasyLottery 目前以 Blazor WebAssembly + Blazorise 提供所有瀏覽器畫面，但後端 REST API、SignalR、Domain、Payment、YouTube、SMTP 與 WebAuthn/Passkey 邊界已經存在。為避免購買或規避 Blazorise 授權，也避免一次性重寫造成回歸風險，採 Strangler Migration，先讓 Vue 接管低風險的公開頁面與登入入口。

## 目標

- 建立可由 CI 建置、可由 ASP.NET Core 同源提供的 Vue 3 + TypeScript + Vite shell。
- 先遷移 `/about`、`/privacy-policy`、`/login`，同時保留 Blazor 根頁與未遷移功能。
- 以既有 Passkey options/verify API 完成登入與首次註冊橋接，不改變 backend protocol。
- 為後續 Admin、Lottery、OBS/SignalR 遷移留下清楚的 phase 邊界與回滾路徑。

## 範圍

- `src/EasyLotteryWeb`：Vue app、router、Pinia、Vuetify、Passkey client、Vitest。
- `src/EasyLotteryAPI`：只新增三個 Vue history fallback routes；不改 API contract。
- `src/EasyLotteryWasm`：保留現有 host，讓 Vue build output 作為同源 static assets 輸出；不移除 Blazorise。
- CI 與 Docker：先建置 Vue，再建置 .NET publish output。
- `tests/e2e`：新增 Vue public route smoke coverage。

## 非目標

- 不移除或替換 EasyLotteryWasm、Blazorise、既有 Blazor pages 或 backend/domain projects。
- 不遷移 Admin Settings、Lottery/Activity、OBS 或 SignalR realtime pages。
- 不變更 Passkey、session-token、Payment、YouTube、SMTP 或 production deployment contract。
- 不執行 production cutover 或寫入 production data/secrets。

## 驗收標準

- [ ] `npm ci`, `typecheck`, `test`, `build` 可在 frontend project 通過。
- [ ] API host 的 `/about`、`/privacy-policy`、`/login` 由 Vue shell 提供，其他既有路由仍由 Blazor 提供。
- [ ] Vue public pages 不初始化或呼叫 authenticated settings endpoint。
- [ ] Login 初始 email 為空；空白或格式錯誤時只顯示 client-side alert，不呼叫 Passkey options API。
- [ ] Passkey login/register 使用既有 `/api/auth/passkey/options` 與 `/api/auth/passkey/verify`，成功後保存原 session token 並回到現有 root。
- [ ] .NET tests/build 與既有 E2E regression 不回歸。
- [ ] OpenSpec、決策、驗證紀錄與 PR 都連回 #252、#255、#266。

## 測試策略

- Vitest：email validation、public route metadata、Passkey client error/serialization boundaries。
- Playwright：Vue About/Privacy/Login 無 settings request、空白/錯誤 email 不發 Passkey request、small viewport 無水平溢出。
- .NET：既有 solution test/build，確認 static fallback 編譯與後端 contract 不變。
- Docker/CI：Node frontend build 先於 .NET publish，確保發布包包含 Vue assets。

## 回滾策略

本階段不修改非 public 的 Blazor route。若 Vue public shell 發生回歸，可 revert Vue fallback mappings 或暫時停用三個 fallback mappings，root 與其他 Blazor 路由仍可服務；Passkey backend 與資料不受影響。

## 觀測性

- Playwright 記錄 `/about`、`/privacy-policy`、`/login` 不應出現 `/settings` request。
- Login client-side rejection 應在未呼叫 `/api/auth/passkey/options` 前完成。
- 伺服器維持既有 status/error logging；本 phase 不增加 credential 或 token logging。
