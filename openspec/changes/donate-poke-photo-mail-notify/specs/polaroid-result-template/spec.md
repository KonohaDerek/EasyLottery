## ADDED Requirements

### Requirement: 拍立得結果模板
系統 SHALL 提供至少一組內建拍立得結果模板，模板 MUST 可設定背景、標題與 fallback 恭喜文案。Donate 拍立得活動 MUST 可選擇模板，未設定或找不到模板時 MUST 使用內建預設模板。

#### Scenario: 使用內建模板
- **WHEN** 拍立得 Donate 活動產生中獎結果
- **THEN** OBS overlay 使用活動選定或預設的拍立得模板顯示結果
