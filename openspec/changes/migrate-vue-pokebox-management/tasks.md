# Tasks

- [x] 記錄 #268 PokeBox 管理切片的範圍、非目標、風險、回滾及驗證計畫。
- [x] 加入既有 PokeTemplate/PokeCell contract 的 TypeScript 型別與純 helper，覆蓋深層 clone、格數調整及 OBS URL。
- [x] 建立受 session guard 保護的 Vue PokeBox 管理頁，保留現有管理 API 操作及錯誤/重試狀態。
- [x] 接入 `/pokebox` Vue route/fallback/Blazor handoff，保留 `/legacy/pokebox`、編輯 query alias 與未遷移 routes。
- [x] 加入 Vitest 與 Playwright 測試，驗證授權、管理操作、錯誤路徑、舊路由與測試 OBS token fragment。
- [x] 執行 Vue tests、typecheck/build、Playwright、.NET build/tests、OpenSpec strict、diff 檢查並記錄結果。
- [x] 主代理獨立檢查行為與 diff，提交、推送 branch 並建立 Ready for review PR #278，關聯 #252/#268。
