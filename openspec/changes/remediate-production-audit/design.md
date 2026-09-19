## 設計

### 路由與版型

新增只提供頂端連結與內容容器的公開版型。`App.razor` 依目前路徑為 `/about` 或 `/privacy-policy` 選用公開版型，其他已登入頁維持 `MainLayout`。如此不會建立 `NavMenu`，也不會觸發其需要管理 session 的設定讀取。

### 登入與首頁驗證

登入元件將 email 初始值改為空字串，新增本地驗證狀態；空白或不合法 email 顯示具 `role="alert"` 的訊息並直接返回。首頁保留既有 validation 規則，但改為只在使用者試圖啟動直播擷取時要求完整驗證，初始空資料不標示為錯誤。

### 文件語意與內容

`index.html` 使用 `zh-Hant` 與產品名稱。缺少主標題的管理頁補上可見或 visually-hidden 的單一 `h1`，不變更既有 `PageTitle`。隱私頁改用繁中並敘明：資料保存在部署者控制的儲存體、資料類型、外部整合、保存與刪除責任，以及使用者聯絡部署者的方式。

### 外部項目

Blazorise 授權橫幅、HTTP→HTTPS 與安全標頭、金流/SMTP/API/Tunnel 設定各自建立 GitHub issue。程式碼不嘗試隱藏授權橫幅或繞過授權，也不寫入 production secrets。

### 測試

優先把可抽出的輸入驗證與公開路由選擇邏輯做成單元可測試的純函式；必要時以 Playwright E2E 驗證未登入公開頁與首頁的顯示狀態。每個修正遵循 RED→GREEN；驗證包含指定測試、完整 `.sln` test、WASM/API build，以及本地或 Ego Lite 的只讀視覺檢查。
