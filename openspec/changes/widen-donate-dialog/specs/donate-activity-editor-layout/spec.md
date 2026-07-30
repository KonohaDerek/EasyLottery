## ADDED Requirements

### Requirement: Donate 活動編輯 Dialog 寬度
系統 SHALL 讓 Donate 活動新增與編輯 Dialog 在寬度至少為 768px 的視窗中使用至少 75% 的視窗寬度；在較窄視窗中 MUST 保留可見邊距且不可產生橫向溢出。

#### Scenario: 桌面編輯活動
- **WHEN** 使用者在桌面視窗開啟新增或編輯 Donate 活動 Dialog
- **THEN** Dialog 寬度至少為視窗寬度的 75%

#### Scenario: 窄螢幕編輯活動
- **WHEN** 使用者在小於 768px 的視窗開啟 Donate 活動 Dialog
- **THEN** Dialog 保留左右安全邊距並可垂直捲動內容
