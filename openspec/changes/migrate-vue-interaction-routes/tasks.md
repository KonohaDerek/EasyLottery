# Tasks

- [x] 核對 Issue #290、Project #1、現有 Blazor pages、REST/SignalR contracts 與 Vue patterns；將 #290 移至 In progress。
- [x] 建立互動管理 Vue API/views，保留管理員 session 與 one-time identity code semantics。
- [x] 建立四個 OBS Vue views 與可測試 SignalR lifecycle，保留透明背景、連線錯誤狀態及 reduced-motion 行為。
- [x] 將六個 Vue routes 與管理導覽註冊，並在 API host 加精確 Vue fallbacks。
- [x] 將六個 Blazor pages 保留於 /legacy/... rollback aliases。
- [x] 補 Vitest、route tests、focused Playwright 與必要 API/Domain regression coverage；另修正 identity-link JSON body binding 以符合既有 Blazor/Vue client payload，並補單次成功／重放拒絕 API 測試。
- [x] 在 Dev Container 完成 focused Playwright UI 執行（3/3）；涵蓋管理路由與 REST、Passkey guard、legacy rollback、identity-link 錯誤/成功呈現、OBS live/empty/reconnect/rejoin/錯誤、大小寫投票與非 Live 通知隱藏。原生 OBS 驗證仍由使用者執行。
- [x] 完成 Vue 102/102、typecheck/build、互動 API/Hub/identity-link .NET 3/3、Playwright 3/3、strict OpenSpec 與 diff 檢查；詳見 `.agents/verification/2026-10-11-issue-290-vue-interaction-routes.md`。
- [x] 建立 `.agents/verification/2026-10-11-issue-290-vue-interaction-routes.md` 驗證紀錄。
- [x] 推送分支並開 Ready-for-review PR #292，關聯 #252/#269/#270 並以 `Closes #290` 於合併時關閉 issue；同步將 Project #1 #290 移至 In review；不合併、不部署。
