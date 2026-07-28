## ADDED Requirements

### Requirement: Donate 通知顯示設定
系統 SHALL 允許使用者為每個 Donate 活動設定是否在 OBS 顯示贊助者、金額、留言與付款方式；該設定 MUST 預設為開啟，並經由 CQRS、事件投影與 YAML 快照保存。

#### Scenario: 保存關閉的通知設定
- **WHEN** 使用者關閉「顯示 Donate 資訊」並儲存活動
- **THEN** 重新開啟活動設定仍顯示為關閉，且活動 payload 保留該設定

#### Scenario: 舊活動資料
- **WHEN** 系統讀取未含通知顯示欄位的既有活動 YAML
- **THEN** 活動以顯示 Donate 資訊的預設值執行

### Requirement: 略過 Donate 通知
系統 SHALL 在通知顯示設定關閉時，於 OBS 收到新的抽獎結果後直接開始所設定的抽獎動畫，不顯示 Donate 資訊卡。

#### Scenario: 關閉通知後收到結果
- **WHEN** 關閉通知顯示的活動收到新的 Donate 抽獎結果
- **THEN** OBS 先進入動畫階段，動畫完成後才揭曉獎項
