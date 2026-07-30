## ADDED Requirements

### Requirement: 刮刮樂動畫場景
系統 SHALL 在 Donate 活動選擇刮刮樂時播放自動刮除銀漆的刮刮樂場景，並在銀漆下方顯示本次抽獎結果。

#### Scenario: 銀漆自動刮除
- **WHEN** Donate OBS 進入刮刮樂抽獎動畫階段
- **THEN** 銀漆隨設定的動畫秒數逐步移除，最後可閱讀底下的結果文字
