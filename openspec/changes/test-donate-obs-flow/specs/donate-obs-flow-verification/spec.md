## ADDED Requirements

### Requirement: Donate OBS 結果生命週期驗證
系統 SHALL 以自動測試驗證 Donate OBS 初始待機、通知、動畫、揭曉與清除流程。測試 MUST 可明確控制目前時間，且不依賴實際等待。

#### Scenario: 初始待機
- **WHEN** OBS 第一次讀取已有歷史抽獎紀錄的活動
- **THEN** 歷史結果只作為游標，OBS 保持透明待機且不顯示結果

#### Scenario: 通知與動畫流程
- **WHEN** 開啟 Donate 資訊顯示的活動收到新結果
- **THEN** 流程依序進入三秒通知、設定秒數的動畫及結果揭曉

#### Scenario: 略過通知流程
- **WHEN** 關閉 Donate 資訊顯示的活動收到新結果
- **THEN** 流程直接進入設定秒數的動畫，再進入結果揭曉

#### Scenario: 結果清除
- **WHEN** 結果揭曉時間到期
- **THEN** OBS 清除目前結果並回到透明待機

### Requirement: Donate 動畫對照驗證
系統 SHALL 以自動測試驗證一番賞、扭蛋、日式滾筒、塞錢箱與刮刮樂均對應唯一 OBS 動畫場景 key 與 CSS class。

#### Scenario: 五種動畫設定
- **WHEN** 活動選擇任一支援的 Donate 動畫
- **THEN** OBS 取得該動畫唯一且正確的場景 key 與 CSS class

### Requirement: Donate 未中獎流程驗證
系統 SHALL 以自動測試驗證未中獎紀錄仍經由相同生命週期揭曉與清除，但不被標示為中獎。

#### Scenario: 銘謝惠顧揭曉
- **WHEN** 新 Donate 結果的 `IsWinning` 為 false
- **THEN** 流程完成動畫與揭曉，並在結果顯示時間到期後清除
