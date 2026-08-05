## ADDED Requirements

### Requirement: 統一測試 OBS 入口

系統 SHALL 在 Donate、戳戳樂與轉盤列表提供單一「開啟測試 OBS」入口，並產生含 `controls=1` 及 control scope token 的 URL。

#### Scenario: 開啟活動測試頁

- **WHEN** 實況主在已啟用的活動或模板按下「開啟測試 OBS」
- **THEN** 開啟的頁面可顯示測試控制項，且可直接被 OBS 擷取

### Requirement: 直接操作戳戳樂

隨機模式的戳戳樂 SHALL 在使用者點擊任一格子時啟動既有隨機揭露流程。

#### Scenario: 點擊隨機戳戳樂格子

- **WHEN** 測試 OBS 頁面處於隨機模式且使用者點擊格子
- **THEN** 系統執行一次隨機揭露，而不要求先按控制列按鈕

### Requirement: 直接操作轉盤

轉盤 SHALL 在使用者點擊轉盤區域時啟動倒數後旋轉；控制按鈕與鍵盤 Enter／Space SHALL 使用相同流程且不可重複觸發。

#### Scenario: 點擊轉盤開始抽獎

- **WHEN** 測試 OBS 頁面未在抽獎中且使用者點擊轉盤
- **THEN** 系統執行既有倒數與旋轉動畫
