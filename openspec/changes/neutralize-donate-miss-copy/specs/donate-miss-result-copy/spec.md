## ADDED Requirements

### Requirement: Donate 未中獎結果文案
系統 SHALL 在 Donate 抽獎記錄為未中獎時，以中性且不含「恭喜」語意的感謝參與文案顯示結果；只有中獎結果才可使用 AI 恭喜文案或中獎結果模板。

#### Scenario: 銘謝惠顧結果
- **WHEN** Donate OBS 揭曉 `IsWinning` 為 false 的抽獎結果
- **THEN** 結果頁顯示銘謝惠顧與感謝參與文案，且文案不含恭喜

#### Scenario: 中獎結果
- **WHEN** Donate OBS 揭曉 `IsWinning` 為 true 的抽獎結果
- **THEN** 結果頁維持既有中獎結果模板與可選 AI 恭喜文案
