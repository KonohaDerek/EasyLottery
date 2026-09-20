# 多平台直播互動與 OBS 設計規格

## 目標

讓主持人可在同一場活動中接收 YouTube、Twitch 與 Discord 的觀眾事件，將跨平台帳號合併為可稽核的統一觀眾身份，並用同一套規則驅動投票、答題、任務、點數、排行榜、抽獎資格與 OBS overlay。

## 非目標

- 不自動合併不同平台帳號；任何合併都必須由主持人操作或由觀眾完成一次性綁定驗證。
- 不以 API Key 作為所有平台的通用授權方式。各 connector 採用平台要求的 OAuth、Bot token、簽章或 API Key。
- 不改動既有 Donate、戳戳樂、轉盤與加班台的活動資料格式；新互動能力以獨立資源逐步整合。
- MVP 不包含公開觀眾帳戶網站、付費商品、跨頻道點數共享或未定義平台。

## 產品流程

```text
平台聊天室／事件
  → Connector 驗證、去重、正規化
  → InteractionEvent
  → Identity Resolver
  → Interaction Round Rule Engine
  → Interaction State + Audit Record
  → 即時事件串流
  → 主持人控制台、平台回覆、OBS overlays
```

1. 主持人在控制台建立或選取一個互動回合，指定可參與的平台、指令、時間、計分與資格條件。
2. Connector 把收到的聊天室訊息或平台事件轉成不含祕密的標準事件。
3. 身份解析器取得平台帳號的統一觀眾檔案；未綁定帳號仍可依回合規則暫時參與，且會保留平台來源。
4. 規則引擎以統一觀眾 ID 做去重、資格、得分、投票與作答判定。
5. 系統寫入可稽核的結果，並把狀態快照與增量事件推送給控制台與 OBS。

## 平台連線

### 共用 Connector 介面

每個 connector 必須實作以下職責：

- 連線狀態與健康檢查。
- 收取平台事件、驗證來源、使用外部事件 ID 去重。
- 轉換為 `InteractionEvent`：平台、頻道／Guild、平台使用者 ID、顯示名稱、事件類型、內容、發生時間與來源事件 ID。
- 在平台能力允許時回覆確認、錯誤或綁定提示。
- 回報授權到期、速率限制及可恢復錯誤。

Connector 不可包含活動規則、點數計算或 OBS 呈現邏輯。

### 平台設定

| 平台 | MVP 事件 | 授權／設定 | 管理介面 |
| --- | --- | --- | --- |
| YouTube | 直播聊天室訊息與可用的公開事件 | API Key 讀取；需要代表頻道操作時以 OAuth 連線 | 頻道／直播選擇、連線狀態、最近事件 |
| Twitch | 聊天訊息與 EventSub 可提供的事件 | OAuth access/refresh token、Client ID、EventSub callback 驗證 | 頻道、權限範圍、到期日、重新授權 |
| Discord | 指定 Guild／Channel 的 Bot 訊息與互動 | Bot token、Guild／Channel 選擇、必要 Bot 權限 | 伺服器、頻道、權限檢查、最近事件 |

秘密採部署者控制的安全儲存，API 回傳僅顯示遮罩與健康狀態。OAuth redirect、state、PKCE（適用時）、refresh token 與 webhook 簽章驗證均由伺服器端處理；瀏覽器與 OBS URL 不攜帶平台祕密。Phase 1 預設使用 YAML repository，因初期資料量低；SQLite 保持為可選 provider，日後 PostgreSQL 以同一 repository contract 新增 adapter，不改變 Domain 或 API 行為。

## 身份與綁定

### 資料模型

- `AudienceProfile`：穩定 ID、顯示名稱、頭像、建立／更新時間、合併狀態。
- `PlatformIdentity`：平台、外部使用者 ID、頻道／Guild 範圍、最近顯示名稱、所屬 `AudienceProfile`。
- `IdentityLinkRequest`：一次性碼雜湊、來源帳號、目標帳號或 profile、到期時間、已使用／撤銷狀態。
- `IdentityMergeAudit`：操作人、來源／目標 profile、原因、時間與可還原資訊。

### 綁定與合併規則

- 觀眾在平台 A 取得短效一次性綁定碼，並在平台 B 輸入；兩個 Connector 確認帳號後才建立連結。
- 主持人可在控制台搜尋後提出合併；必須二次確認並寫入審計紀錄。
- 合併不刪除來源 identity 與歷史事件。解除綁定只影響未來計分；既有回合結果維持不可變歷史。
- 同一統一身份在單一回合的投票／答題／資格以 `AudienceProfileId` 去重，防止跨平台重複參與。

## 互動回合與規則

`InteractionRound` 具備狀態 `Draft`、`Scheduled`、`Live`、`Paused`、`Settled`、`Cancelled`，並記錄參與平台、活動模板、開始／結束時間、主持人與版本。

第一批可組合規則：

- **簽到／任務**：關鍵字或指令、每日／每回合次數、點數與抽獎 ticket。
- **投票**：選項、倒數、每 profile 一票、是否允許改票、結算方式。
- **答題**：題目、接受答案、限時、首次答對分數、連勝與提示。
- **點數與排行榜**：本回合、單場直播、當日三種範圍；手動加扣分均須寫入稽核。
- **抽獎資格**：以 ticket、任務、點數門檻、參與事件或主持人名單產生既有抽獎的候選快照。

主持人可開始、暫停、延長、結算、作廢回合，以及手動調整分數。作廢不抹除事件，而是以明確的狀態變更與理由保留。

## OBS 與即時同步

每種呈現是獨立、透明背景、read-only scoped token 的 browser source：

1. `round-status`：目前回合、參與平台與倒數。
2. `question-vote`：題目／投票選項、即時票數及結算動畫。
3. `leaderboard`：前 N 名、分數變化與個人連勝提示。
4. `interaction-notice`：答對、任務完成、抽獎資格與得獎通知。

控制權只存在主持人控制台與具 `control` scope 的測試頁。OBS reconnect 時先讀取最新狀態快照，再訂閱增量事件；斷線不能造成重複計分。沿用既有 scoped OBS token 的資源隔離與透明／reduced-motion 行為。

## 可靠性、安全與觀測性

- 以 `(platform, channelScope, externalEventId)` 做持久化去重；所有規則判定採冪等寫入。
- Connector 依平台建議的速率與 retry-after 執行退避；暫時失敗會標示 degraded，不阻塞其他平台。
- Webhook 與 OAuth callback 驗證簽章／state；平台事件與敏感欄位不寫入一般應用程式日誌。
- 連線狀態、最近成功事件、延遲、去重數、規則拒絕原因、OBS 訂閱數和回合狀態納入監控。
- 主持人可停用單一 Connector；進行中的回合會顯示受影響平台，並可由主持人選擇繼續、暫停或結算。

## MVP 切分與驗收

### Phase 1：核心與雙平台

- `InteractionEvent`、觀眾身份／手動合併、YouTube 與 Twitch connector。
- 簽到、投票、答題、點數、排行榜與抽獎資格快照。
- 主持人回合控制台與四個 OBS overlay。
- 驗收：同一觀眾綁定兩平台後只能投一票；OBS 在重連後呈現正確回合；重送同一平台事件不會重複加分。

### Phase 2：Discord 與自助綁定

- Discord Bot connector、一次性雙平台綁定碼、解除綁定與完整審計查詢。
- 驗收：綁定碼過期／重放均被拒絕；主持人可在不中斷回合下停用 Discord。

### Phase 3：任務模板與營運能力

- 可重用活動模板、進階資格、主持人操作紀錄、健康儀表板與匯出。
- 驗收：模板可複製並可安全修改；平台授權失效與速率限制在設定頁可見且具可行的恢復操作。

## 測試與回滾

- Domain：身份合併／解除、規則優先順序、投票去重、答題計分、事件冪等。
- API：OAuth state、webhook 簽章、權限邊界、秘密遮罩、Connector health。
- E2E：主持人建立回合、模擬多平台事件、綁定、OBS reconnect、暫停與結算。
- 回滾：新互動資料和既有抽獎資料分開保存；關閉 connector 或 feature flag 即停止接收事件，不刪除歷史。各 overlay 在互動功能停用時回到透明 idle。
