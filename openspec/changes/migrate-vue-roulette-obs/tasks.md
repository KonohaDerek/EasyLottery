# 任務

- [x] 加入官方 `@microsoft/signalr` runtime dependency，並實作最小 Live Draw client（既有 hub、Join、事件、重連重新 Join、REST snapshot）。
- [x] 實作 OBS session token fragment bootstrap 與可測的 URL 清理；不將 token 記錄到 URL 或 console。
- [x] 建立 Vue 轉盤 OBS route/view，保持讀取/控制模式、模板呈現、旋轉/結果/音效/結果記錄及透明背景。
- [x] 讓 API host fallback 指向 Vue，並保留 Blazor `/legacy/obs/roulette/{publicId}` rollback alias。
- [x] 增加 Vitest 與 Playwright 覆蓋：token、route、live-draw 同步/重連、鍵盤控制、錯誤狀態與 rollback alias。
- [x] 在專案 Dev Container 執行 Vue test/typecheck/build、相關 Playwright、.NET Release build/tests、OpenSpec strict 與 diff checks。
- [x] 更新 `.agents/state/current.md` 與 verification record，完成第二輪/獨立審查，再建立 Ready-for-review PR；只引用 #269/#252，不關閉 Phase 4 issue。
