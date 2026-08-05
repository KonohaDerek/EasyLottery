## ADDED Requirements

### Requirement: 管理編輯 Dialog 寬度

系統 SHALL 讓 Donate、戳戳樂與轉盤的新增／編輯 Dialog，在寬度至少為 768px 的視窗中使用 75% 的視窗寬度；小於 768px 時 MUST 保留左右安全邊距且不可產生橫向溢出。

#### Scenario: 桌面編輯模板

- **WHEN** 使用者在桌面視窗開啟 Donate、戳戳樂或轉盤的新增／編輯 Dialog
- **THEN** 外層 Dialog 寬度為視窗寬度的 75%，且欄位可在多欄版面中輸入

#### Scenario: 窄螢幕編輯模板

- **WHEN** 使用者在小於 768px 的視窗開啟任一管理編輯 Dialog
- **THEN** Dialog 保留左右安全邊距並可垂直捲動內容

### Requirement: OBS 資產選擇器寬度

資產選擇器 Dialog SHALL 使用與管理編輯 Dialog 相同的寬版規則，讓資產縮圖與檔名可讀。

#### Scenario: 選擇本機資產

- **WHEN** 使用者在任一管理表單開啟圖片或音效資產選擇器
- **THEN** 資產選擇器使用 75vw 寬度，並在窄螢幕改為安全邊距內的滿寬
