## ADDED Requirements

### Requirement: OBS 極簡呈現
OBS 加班頁 SHALL 只保留直播觀看所需的必要資訊，不得同時顯示控制用表單、設定頁等級的多區塊操作元件或會干擾視線的額外摘要卡。

#### Scenario: OBS 只保留核心資訊
- **WHEN** 使用者開啟 `http://localhost:18930/obs/overtime`
- **THEN** 畫面 SHALL 只呈現未開始時的預計關台時間與開始按鈕，或開始後的倒數文字與單行 donate/Support 事件資訊

### Requirement: OBS 啟動控制
OBS 加班頁 SHALL 提供一個開始按鈕以啟動倒數，該按鈕 SHALL 是唯一的開始控制。

#### Scenario: OBS 按下開始
- **WHEN** 使用者在 OBS 加班頁按下開始按鈕
- **THEN** 系統 SHALL 以目前預計關台時間作為倒數基準並開始倒數

#### Scenario: OBS 按下停止
- **WHEN** 使用者在 OBS 加班頁按下停止按鈕
- **THEN** 系統 SHALL 停止倒數並回到僅顯示預計關台時間的待命狀態

### Requirement: 倒數文字
OBS 加班頁 SHALL 顯示可隨時間更新的倒數文字，用於表達從加班開始到預計結束的剩餘時間。

#### Scenario: 倒數隨時間變化
- **WHEN** 加班已開始且預計結束時間存在
- **THEN** 倒數文字 SHALL 隨現在時間變化而更新

### Requirement: donate 單行訊息
當收到 SuperChat 或綠界 Donate 時，OBS SHALL 以單行訊息顯示該事件，訊息內容不得超過一行主要文案。

#### Scenario: 顯示單行 donate 訊息
- **WHEN** 新的 donate 事件到達
- **THEN** OBS SHALL 顯示一行包含贊助者名稱、來源與加班資訊的提示

### Requirement: donate 自動消失
donate 單行訊息 SHALL 在預定秒數內自動消失，以避免持續遮擋直播內容。

#### Scenario: 訊息到期消失
- **WHEN** donate 訊息顯示達到設定秒數
- **THEN** 系統 SHALL 自動將該訊息從 OBS 畫面移除

### Requirement: donate 延長加班
當 donate 事件符合加班規則時，系統 SHALL 依金額對應規則自動延長預計結束時間，進而更新 OBS 倒數。

#### Scenario: donate 觸發延長
- **WHEN** 收到符合規則的 donate 事件
- **THEN** 系統 SHALL 根據規則延長預計結束時間並反映到倒數文字

### Requirement: 事件攜帶結束時間
每筆加班事件 SHALL 攜帶最新的結束時間，OBS 加班頁 SHALL 依最新事件中的結束時間作為倒數來源。

#### Scenario: OBS 以事件結束時間為準
- **WHEN** OBS 頁讀取到包含 `SessionEndAtUtc` 的最新事件
- **THEN** OBS SHALL 顯示該事件對應的剩餘時間

### Requirement: 示範事件同步時間
當使用者在設定頁送出示範 SuperChat 或綠界 Donate 時，事件時間戳與加班時間基準 SHALL 使用同一個 nowUtc，以避免 OBS 倒數與事件時間不一致。

#### Scenario: 示範送出同步時間
- **WHEN** 使用者送出示範 SuperChat 或綠界 Donate
- **THEN** 系統 SHALL 使用同一個時間點寫入事件時間與加班延長計算

### Requirement: 設定頁僅保留配置
加班設定頁 SHALL 僅保留配置與預覽用途，不得再承擔 OBS 即時展示控制的主要責任。

#### Scenario: 設定頁不顯示操作型控制
- **WHEN** 使用者查看加班設定頁
- **THEN** 頁面 SHALL 只提供預計關台時間與加班規則等配置，不再提供 OBS 用的開始控制區塊
