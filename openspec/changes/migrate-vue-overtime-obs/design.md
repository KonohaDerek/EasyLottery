# 設計

## 路由與呈現

- Vue route 為精確的 /obs/overtime，不使用管理員 shell；API host 僅為此 route 提供 Vue shell fallback。
- Blazor component 保留 /legacy/obs/overtime rollback route。
- controls query 只切換輔助控制 UI，不視為授權；所有控制仍由 read/control OBS token 和 server 驗證。
- 保留透明背景、倒數/進度、idle/ended 隱藏行為、support event、完成動畫與目前選用的主題/文字動畫。

## Token、settings 與 feed

- 頁面啟動使用共用 bootstrapObsSessionToken()，從 URL fragment 匯入 token、保留非憑證 query，並以 history.replaceState 清除憑證。
- GET /settings 讀取 overtime/default resource projection；使用既有 yaml 依賴解析 EasyLotteryConfigDocument，僅讀取/更新 OvertimeOverlay 欄位。
- PUT /settings 傳送修改後的 YAML projection，由現有 ObsSettingsProjectionService 將更新限制在 overtime/default。不得用 control token 呼叫 PUT /api/settings/overtime，因該資源 PUT 僅允許管理員。
- GET/PUT /api/overtime-feed 沿用現有 token header 和 OvertimeSupportEvent[] payload；開始/停止與測試通知延續既有 feed 寫入行為。
- 不在 browser 端新增 scope 判斷作為安全控制；控制 query 不是權限來源，API 授權結果必須可見且不可樂觀地假裝成功。

## SignalR 與 session 狀態

- 使用官方 @microsoft/signalr client 連線 /hubs/overtime，呼叫 Join(token)，監聽 OvertimeStateChanged 與 OvertimeFeedChanged。
- Hub event 不攜帶完整狀態；收到事件後重新取得 settings/feed，重連後重新 Join 並重新取得 snapshot。
- 以單一飛行中的 refresh 加一次合併重讀避免慢速請求持續丟棄快照；每 5 秒低頻輪詢 settings/feed，因管理頁設定更新不一定送出 hub event，即時連線時也需保持同步。
- 控制動作以最新 YAML projection 合併欄位 patch，不把載入時的整份舊 settings 寫回；完成狀態寫入失敗時，後續輪詢重試。到期判定使用 settings 或 feed marker 的有效結束時間。
- 倒數顯示沿用既有 OvertimeSessionTimeCalculator 的運算語意，以純函數和單元測試保護 pause/resume、progress 與 end transitions。

## 驗證、觀測與回復

- Vitest 覆蓋 YAML projection 更新、fragment 清理、read/control API 錯誤、倒數邊界、更新競態，以及 SignalR reconnect 後重新 Join/refresh 和失敗處理。
- Playwright 使用不同的 read/control token browser context，驗證 start/pause/resume/end、即時 hub/feed 更新、透明畫面與 token 清理；測試必須還原 settings/feed。瀏覽器離線事件不穩定地觸發 SignalR reconnect，因此重連生命週期由 Vitest 驗證，不宣稱 E2E 模擬斷網。
- 控制模式顯示連線/錯誤狀態；`motion=reduce` 停用過場與動畫；一般 overlay 不顯示診斷資訊，任何路徑不輸出 session token。
- 對 /obs/overtime 路由做精確 SPA fallback 並留著 legacy alias；回復不涉及資料遷移。
- 使用 Chromium viewport 驗證透明和響應版面；實際 OBS Browser Source smoke 若不可執行，需記錄限制。
