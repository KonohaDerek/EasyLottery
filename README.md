# EasyLottery

EasyLottery 是一套以 **Blazor WebAssembly + Domain Service** 組成的直播活動抽獎工具，主要用來支援 YouTube 直播互動、戳戳樂、轉盤抽獎、活動結果記錄與系統設定。

## 系統目標

這個系統的目的，是讓直播或活動主持人可以在同一套工具中完成以下工作：

- 匯入 YouTube 會員名單與授權 YouTube 功能
- 管理抽獎活動參加者與獎項
- 使用戳戳樂（Poke Box）或轉盤（Roulette）進行抽獎
- 將已結束的活動結果集中保存與檢視
- 管理部分系統設定與外部服務設定

## 核心功能

### 1. YouTube 會員與授權

- 支援 YouTube OAuth 授權登入
- 可檢查目前是否已完成授權
- 可匯入 YouTube 會員名單
- API 端提供會員資料查詢能力

### 2. 首頁抽獎流程

首頁是主要操作入口，包含：

- 匯入 YouTube 會員名單
- 監看直播聊天室關鍵字
- 新增獎項
- 新增參加者
- 設定中獎倍率
- 執行抽獎
- 管理得獎名單

### 3. 戳戳樂（Poke Box）

戳戳樂功能提供模板式的活動管理，支援：

- 建立、編輯、刪除模板
- 載入預設模板
- 預覽與編輯模板
- 匯入 / 匯出 JSON 模板
- 隨機或手動戳取格子
- 設定格子內容、圖片、顏色、動畫與版面

### 4. 轉盤抽獎（Roulette）

轉盤功能提供可配置的抽獎模板，支援：

- 建立、編輯、刪除模板
- 載入預設模板
- 預覽與編輯模板
- 匯入 / 匯出 JSON 模板
- 設定每個格子的標題、圖片與機率
- 設定轉動時間、 easing 曲線、背景、指針與音效
- 執行加權隨機抽獎

### 5. 活動結果

活動結果頁面用來集中查看歷史紀錄，包含：

- 已結束的戳戳樂活動
- 已結束的轉盤活動
- 活動總數統計
- 最近活動時間
- 每筆活動的摘要與明細

### 6. 系統設定

系統目前提供部分設定入口，例如：

- YouTube 登入設定
- 支付設定頁面（目前仍在開發中）

## 系統架構

專案分成兩個主要層級：

- `EasyLotteryDomain`：核心商業邏輯、資料模型與服務
- `EasyLotteryWasm`：Blazor WebAssembly 前端，提供操作介面

另外有一個 Web Host 與測試專案：

- `EasyLotteryWeb`：ASP.NET Core Web Host，提供前端靜態檔、YAML 設定與加班事件 API
- `EasyLotteryDomainTests`

## 資料與儲存方式

- 系統主要使用 YAML 作為設定資料來源，儲存於 Web Host 的 `App_Data/easy-lottery.yaml`
- `EasyLotteryWeb` 提供同源 API，所有瀏覽器與 OBS 讀寫同一份 YAML
- Domain 層負責模板、活動結果與抽獎邏輯
- 模板與活動資料會在本地設定中持續保存

## 主要頁面

- `/`：首頁抽獎操作
- `/activity-results`：活動結果
- `/pokebox`：戳戳樂模板列表
- `/roulette`：轉盤模板列表
- `/system/youtube-login`：YouTube OAuth 授權
- `/system/payment`：支付設定（開發中）

## 技術堆疊

- .NET 8
- Blazor WebAssembly
- Blazorise
- Serilog
- YamlDotNet
- Google.Apis.YouTube.v3
- MSTest

## 開發與執行

### 前置需求

- .NET SDK 8.0
- 支援 Blazor WebAssembly 的瀏覽器

### 執行 Web Host

```bash
dotnet run --project src/EasyLotteryWeb/EasyLotteryWeb.csproj --launch-profile EasyLotteryWeb
```

### 以 Docker 執行

```bash
docker compose up --build
```

設定資料會保存於 Docker volume `easy_lottery_data`。

### 執行測試

```bash
dotnet test EasyLottery.generated.sln
```

## 開發流程

如果你要依照這個專案的標準流程進行開發，請看：

- [EasyLottery 開發憲法](docs/constitution.md)
- [EasyLottery 開發流程](docs/development-process.md)
- [通用開發流程（跨專案可重用）](temp/development-process.md)

## 測試專案

- `tests/EasyLotteryDomainTests`：驗證抽獎、戳戳樂與輔助服務的核心邏輯

## 功能摘要

如果用一句話描述這個系統：

> EasyLottery 是一個面向直播與活動抽獎場景的整合工具，提供 YouTube 整合、抽獎流程、模板化活動管理與結果追蹤。
