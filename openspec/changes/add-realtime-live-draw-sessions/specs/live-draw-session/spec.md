# Live Draw Session

## ADDED Requirements

### Requirement: Server-authoritative draw

戳戳樂與轉盤的正式 OBS 流程 SHALL 由伺服器決定唯一結果，控制端不得在瀏覽器自行產生正式結果。

#### Scenario: Submit a roulette spin

- **WHEN** 控制頁對指定工作階段送出轉盤命令
- **THEN** 伺服器依模板權重決定一次結果
- **AND** 同一工作階段的所有連線收到相同項目

#### Scenario: Submit a poke command

- **WHEN** 控制頁指定格子或要求隨機戳取
- **THEN** 伺服器只揭露一個尚未揭露的有效格子
- **AND** 揭露結果持久化至模板資料

### Requirement: Realtime synchronized phases

系統 SHALL 對同一工作階段廣播倒數、動畫及結果階段。

#### Scenario: Separate control and OBS browsers

- **WHEN** 控制頁送出抽獎命令
- **THEN** 已加入該工作階段群組的 OBS 頁依序收到階段更新

### Requirement: Recoverable session state

OBS 頁 SHALL 在首次載入及 SignalR 重連後讀取最新工作階段快照。

#### Scenario: Reload after result

- **WHEN** OBS 頁在抽獎結果產生後重新載入
- **THEN** 畫面還原最近結果或已揭露狀態

### Requirement: Isolated opaque sessions

每個模板 SHALL 使用持久化 GUID 公開識別碼，SignalR 訊息 SHALL 只送到對應種類與 GUID 的群組。

#### Scenario: Two active sessions

- **WHEN** 兩個不同 GUID 的工作階段同時連線
- **THEN** 任一工作階段的命令不會更新另一工作階段

#### Scenario: Open a new OBS URL

- **WHEN** 使用者從管理頁開啟 OBS
- **THEN** 路徑使用 GUID 而非模板流水號
