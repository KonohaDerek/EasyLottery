# Tasks

- [x] 建立 Vue Roulette 管理切片範圍、風險、決策紀錄與驗證計畫。
- [x] 加入既有 Roulette contract 的 TypeScript 型別與純 helper，覆蓋深拷貝、格數調整及 OBS URL。
- [x] 建立受 session guard 保護的 Vue Roulette 管理頁，保留現有管理 API 操作及錯誤/重試狀態。
- [x] 接入 `/roulette` Vue route/fallback/Blazor handoff，保留 `/legacy/roulette` 與未遷移子路由。
- [x] 擴充 OBS 資產選擇器以支援現有 Audio 資產，並加入必要單元與 Playwright 測試。
- [x] 執行 Vue tests、typecheck/build、Playwright、OpenSpec strict、diff 檢查並記錄結果。
- [x] 主代理獨立檢查行為與 diff，提交、推送 branch 並建立 Ready for review PR，關聯 #252/#268。
