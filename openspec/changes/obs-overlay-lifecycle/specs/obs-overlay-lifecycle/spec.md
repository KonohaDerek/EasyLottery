## ADDED Requirements

### Requirement: Donate OBS 新結果生命週期
系統 SHALL 讓 Donate OBS 在頁面工作階段初始化時忽略既有抽獎紀錄，並且只展示初始化完成後新產生的該活動抽獎結果。可視結果 MUST 在活動設定的顯示秒數到期後自動清除。

#### Scenario: 初次開啟含有歷史紀錄的 Donate OBS
- **WHEN** Donate OBS 載入且活動已有一筆或多筆歷史抽獎紀錄
- **THEN** 頁面不會顯示任何歷史結果，且維持透明待機狀態

#### Scenario: 新 Donate 結果送達
- **WHEN** Donate OBS 初始化完成後收到同一活動的新抽獎紀錄
- **THEN** 頁面顯示該新結果，並在設定的顯示秒數後自動回到透明待機狀態

### Requirement: Donate 結果顯示秒數設定
系統 SHALL 允許使用者在 Donate 活動設定 3 至 300 秒的結果顯示時間，未設定或舊資料的預設值 MUST 為 15 秒。設定值 MUST 經由 CQRS 保存、事件投影與 YAML 快照保留。

#### Scenario: 保存有效顯示秒數
- **WHEN** 使用者將 Donate 活動的結果顯示秒數設定為有效值並儲存
- **THEN** 重新開啟活動編輯視窗時會顯示相同的設定值，且 Donate OBS 使用此值控制新結果顯示時間

#### Scenario: 保存無效顯示秒數
- **WHEN** 用戶端或 API 提交小於 3 或大於 300 的結果顯示秒數
- **THEN** 系統拒絕保存並回傳可理解的驗證訊息

### Requirement: OBS 透明待機狀態
系統 SHALL 讓所有 OBS 路由在載入中、沒有可展示結果或找不到對應設定時，不輸出待機文字、錯誤面板或不透明全頁背景。共用 OBS Layout MUST 清除全域頁面背景與背景偽元素。

#### Scenario: OBS 空白待機
- **WHEN** 任一 OBS 路由尚未有可展示的內容
- **THEN** 擷取來源只輸出透明畫布，不會出現載入、等待或找不到設定文字

#### Scenario: OBS 有可展示內容
- **WHEN** OBS 路由進入既有的活動展示或新 Donate 結果展示狀態
- **THEN** 既有的活動內容與結果仍會正常顯示在透明畫布上
