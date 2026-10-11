## ADDED Requirements

### Requirement: Vue PokeBox OBS 即時呈現

系統 SHALL 由 Vue 呈現既有 `/obs/pokebox/{publicId}` OBS URL，沿用既有 API、SignalR 與 OBS session token contract，並保留 Blazor rollback route。

#### Scenario: 載入 OBS overlay
- **WHEN** Browser Source 開啟有效的 PokeBox 公開 URL，並提供 read scope token
- **THEN** Vue 載入該模板與持久揭露格，以透明背景顯示正確網格，不啟動 Blazor WebAssembly runtime

#### Scenario: 安全清除 session token
- **WHEN** OBS view 從 URL fragment/query 取得 session token
- **THEN** Vue 將 token 放入既有安全儲存機制並清除 URL 中的憑證，不將 token 輸出到 log

#### Scenario: 不同 browser 共用伺服器抽選狀態
- **WHEN** 控制端以 control scope token 戳選定格或隨機戳格
- **THEN** Vue 呼叫既有 Live Draw command，伺服器決定結果，控制端和加入同一 session 的 OBS view 顯示相同階段與揭露格

#### Scenario: 重連恢復
- **WHEN** SignalR 連線重新建立
- **THEN** Vue 重新加入 `pokebox` session 並讀取權威 REST snapshot，舊 revision 不覆蓋更新狀態

#### Scenario: 只讀 token 不可控制
- **WHEN** OBS view 使用 read-only token，或只設定 `controls=1` 但 token 沒有 control scope
- **THEN** poke/reset/record 等受保護操作不會成功，授權由 server contract 決定而非 query string

#### Scenario: 保留回退與結果操作
- **WHEN** 使用者透過舊 Blazor 路徑開啟 overlay，或在有效控制 session 中記錄/重設活動
- **THEN** legacy alias 可載入 Blazor，且 Vue 記錄/通知/重設沿用現有 REST 行為
