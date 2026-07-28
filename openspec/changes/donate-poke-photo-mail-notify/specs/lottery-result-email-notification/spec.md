## ADDED Requirements

### Requirement: 結果通知寄送
系統 SHALL 在收件人與完整 SMTP 設定存在時寄送活動與 Donate 抽獎結果通知；寄信失敗 MUST 不影響已完成的抽獎結果。

#### Scenario: SMTP 設定完整
- **WHEN** 抽獎完成且通知信箱及 SMTP 設定完整
- **THEN** 系統寄送包含活動或 Donate 結果的通知信

#### Scenario: SMTP 設定缺少或寄送失敗
- **WHEN** SMTP 設定不完整或寄送程序發生錯誤
- **THEN** 系統略過或記錄寄信失敗，並保留抽獎結果
