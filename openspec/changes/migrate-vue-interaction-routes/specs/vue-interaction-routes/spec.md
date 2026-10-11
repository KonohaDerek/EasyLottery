# Vue 互動與 OBS 路由規格

## ADDED Requirements

### Requirement: 互動管理頁使用既有 REST 與 session

Vue MUST 提供 /interactions/rounds 和 /interactions/identity-link，並保留既有管理員登入要求。回合頁 MUST 透過既有 REST endpoints 建立、列出與轉換回合；身份頁 MUST 以既有一次性 code endpoint 完成綁定。

#### Scenario: 回合管理

- **WHEN** 已登入管理員開啟互動回合頁
- **THEN** Vue 從既有 rounds endpoint 載入回合並可建立草稿
- **AND** 依回合狀態顯示開始、暫停與結算操作
- **AND** request 使用現有 session header，不改變服務端 DTO 或狀態規則

#### Scenario: 一次性身份碼錯誤

- **WHEN** code 無效、過期或已使用
- **THEN** 頁面以安全且可理解的訊息回覆
- **AND** 不回顯 code、不記錄 code、不改變身份綁定 protocol

### Requirement: Interaction OBS 使用既有 SignalR hub

四個 OBS routes MUST 維持現有 hub /hubs/interactions、JoinRound(roundId, token) 與 InteractionStateChanged snapshot 契約。reconnect 後 MUST 再次 JoinRound 並更新最新狀態；斷線、加入失敗或空 snapshot 必須有可辨識的畫面狀態。

#### Scenario: 即時回合更新與重連

- **WHEN** OBS route 以該 round 的 read-scoped token 連線
- **THEN** client 加入 round 並呈現初始 snapshot 與後續 InteractionStateChanged
- **WHEN** SignalR 自動重連成功
- **THEN** client 再次加入 round 並取得新的 authoritative snapshot

#### Scenario: 無效 OBS token 或回合不存在

- **WHEN** hub 拒絕 token 或 snapshot 為 null
- **THEN** overlay 呈現不洩漏憑證的錯誤/空狀態
- **AND** 不把 token 寫入 URL query、console 或診斷輸出

### Requirement: Vue/Blazor 漸進切換與 OBS 可讀性

正式六條 routes MUST 由 Vue shell 呈現且支援直接導航/重新整理；對應 Blazor pages MUST 保留 /legacy/... rollback aliases。OBS overlay MUST 透明、可讀，動畫 MUST 尊重 prefers-reduced-motion。

#### Scenario: 正式路由與 rollback

- **WHEN** 使用者直接開啟或重新整理任一正式 route
- **THEN** API host 回傳 Vue shell，頁面不載入 Blazor runtime
- **WHEN** 使用者開啟對應 /legacy/... route
- **THEN** 原 Blazor page 仍可用於 rollback

#### Scenario: Reduced motion

- **WHEN** OBS browser 要求 reduced motion
- **THEN** 非必要 CSS 動畫停用
- **AND** 互動資料和連線狀態仍完整呈現
