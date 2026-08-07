# E2E 回歸測試需求

## ADDED Requirements

### Requirement: OBS workflow regression coverage
系統 MUST 以瀏覽器測試驗證 Donate、戳戳樂及轉盤的正式 OBS 頁與控制頁可透過各自 token 載入，且控制頁操作會同步到正式頁。

#### Scenario: Donate test notification
- **WHEN** 控制頁送出測試贊助者、金額、留言與支付方式
- **THEN** 正式 OBS 頁顯示通知資訊並進入抽獎結果流程

#### Scenario: Poke direct interaction and reset
- **WHEN** 控制頁點擊戳戳樂格子並執行重設
- **THEN** 正式 OBS 頁顯示獎項，重設後回到未揭示狀態

#### Scenario: Roulette keyboard interaction
- **WHEN** 控制頁聚焦轉盤並按下 Enter
- **THEN** 正式 OBS 頁顯示抽獎結果

### Requirement: Diagnostic artifacts
測試 MUST 在失敗時保留 Playwright trace、截圖或影片，以及瀏覽器 console/page error 記錄，供 CI 診斷。

#### Scenario: Failed browser test
- **WHEN** 任一 E2E assertion 失敗
- **THEN** CI artifact 包含該測試的 trace、視覺快照與 console log

