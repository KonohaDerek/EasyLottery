## ADDED Requirements

### Requirement: Donate 分階段揭曉流程
系統 SHALL 在收到新 Donate 抽獎結果時，依序顯示贊助通知、抽獎動畫與結果揭曉。獎項名稱與圖片 MUST 在抽獎動畫結束前保持隱藏。

#### Scenario: 新 Donate 結果到達
- **WHEN** Donate OBS 收到工作階段開始後的新抽獎結果
- **THEN** OBS 先顯示贊助者、金額、付款方式與留言，再播放所選動畫，最後顯示抽獎結果

#### Scenario: 動畫期間
- **WHEN** Donate OBS 處於抽獎動畫階段
- **THEN** 獎項名稱、獎品圖片與中獎祝賀文不會顯示

### Requirement: Donate 動畫秒數設定
系統 SHALL 允許使用者為每個 Donate 活動設定 3 至 30 秒的抽獎動畫時間，未設定或舊資料 MUST 使用 8 秒預設值；設定值 MUST 經由 CQRS、事件投影與 YAML 快照保存。

#### Scenario: 保存動畫秒數
- **WHEN** 使用者設定有效動畫秒數並儲存 Donate 活動
- **THEN** 重新開啟設定時會保留該值，且 OBS 的抽獎動畫階段持續相同秒數

#### Scenario: 無效動畫秒數
- **WHEN** 使用者提交小於 3 或大於 30 的動畫秒數
- **THEN** 系統拒絕保存並提供可理解的驗證訊息
