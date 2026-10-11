# Tasks

- [x] 核對 Issue #290、Project #1、現有 Blazor pages、REST/SignalR contracts 與 Vue patterns；將 #290 移至 In progress。
- [ ] 建立互動管理 Vue API/views，保留管理員 session 與 one-time identity code semantics。
- [ ] 建立四個 OBS Vue views 與可測試 SignalR lifecycle，保留透明背景、連線錯誤狀態及 reduced-motion 行為。
- [ ] 將六個 Vue routes 與管理導覽註冊，並在 API host 加精確 Vue fallbacks。
- [ ] 將六個 Blazor pages 保留於 /legacy/... rollback aliases。
- [ ] 補 Vitest、route tests、focused Playwright 與必要 API/Domain regression coverage。
- [ ] 在 Dev Container 執行 Vue tests/typecheck/build、互動 .NET tests、focused E2E、strict OpenSpec 與 diff review；記錄原生 OBS 驗證由使用者執行。
- [ ] 建立驗證/交付紀錄，推送分支並開 Ready-for-review PR，關聯 #252/#269/#270 並關閉 #290；不合併、不部署。
