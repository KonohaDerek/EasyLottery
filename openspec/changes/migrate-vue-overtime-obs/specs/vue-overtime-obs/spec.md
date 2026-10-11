## ADDED Requirements

### Requirement: Vue Overtime OBS overlay

系統 SHALL 由 Vue 呈現既有 /obs/overtime OBS URL，沿用現有 overtime REST、SignalR 和 scoped OBS session contracts，並保留 Blazor rollback route。

#### Scenario: 載入 OBS overlay
- **WHEN** Browser Source 開啟 /obs/overtime 並提供 overtime/default read token
- **THEN** Vue 讀取 scoped settings/feed 並顯示既有加班台狀態，不啟動 Blazor WebAssembly runtime

#### Scenario: 安全處理 session token
- **WHEN** Vue 從 URL fragment 取得 OBS session token
- **THEN** 將 token 匯入既有安全儲存方式並清除 URL 憑證，不記錄 token

#### Scenario: 控制 session
- **WHEN** 控制頁以 overtime/default read/control token 執行 start、pause、resume、end 或測試通知
- **THEN** 使用既有受 scope 限制的 settings/feed endpoint；read-only token 或未授權操作不能改變 server state

#### Scenario: SignalR 通知與重連
- **WHEN** overtime hub 發出 OvertimeStateChanged/OvertimeFeedChanged，或連線重新建立
- **THEN** Vue 重新取得權威 settings/feed；重連後重新 Join(token)，較舊的請求結果不覆蓋較新的狀態

#### Scenario: 管理設定更新與慢速讀取
- **WHEN** 管理頁更新 settings 但沒有 hub event，或快照讀取慢於輪詢間隔
- **THEN** overlay 仍會在低頻同步最新資料，序列化讀取並最多等待一次合併補讀；更新期間的新要求另排背景刷新，不丟棄所有成功快照或卡住等待中的控制動作

#### Scenario: 到期完成與重試
- **WHEN** running session 的有效結束時間來自 settings 或 feed marker，且完成狀態寫入暫時失敗
- **THEN** overlay 顯示到期狀態並於後續輪詢重試；寫入前重新確認 server session 仍 running 且時間已到

#### Scenario: 降低動態效果
- **WHEN** Browser Source URL 包含 `motion=reduce`
- **THEN** 通知、進度轉場與完成動畫使用近乎即時的動畫時長，並保留透明背景

#### Scenario: 保留回退路徑
- **WHEN** 操作者使用 /legacy/obs/overtime
- **THEN** 原 Blazor overtime OBS page 仍可載入作為 Vue 切片回復入口
