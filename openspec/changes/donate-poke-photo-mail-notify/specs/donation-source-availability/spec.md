## ADDED Requirements

### Requirement: 斗內來源只能在可用設定下啟用
系統 SHALL 僅在來源的目前環境連線設定完整時允許啟用綠界、藍新、oen.tw 與 Twitch 小奇點；YouTube SuperChat MUST 僅在 YouTube 設定完整時允許啟用。儲存設定時 MUST 強制關閉任何沒有有效設定的來源。

#### Scenario: 設定不完整的來源
- **WHEN** 使用者尚未填妥某來源必要設定
- **THEN** 該來源的啟用控制不可使用且儲存後保持停用

#### Scenario: 設定完整的來源
- **WHEN** 使用者填妥目前環境所需設定
- **THEN** 該來源可被啟用並保存啟用狀態
