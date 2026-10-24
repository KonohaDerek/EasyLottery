# Donate 歷史查詢

## 新增需求

### Requirement: Admin can query persisted Donate draw records

系統 MUST 讓管理員唯讀查詢已持久化的 Donate 抽獎紀錄，並以日期區間、活動、結果類型及關鍵字篩選。結果 MUST 以抽獎時間新到舊排序，並提供有上限的穩定分頁。

#### Scenario: 管理員瀏覽 Donate 歷史
- GIVEN 管理員已登入且儲存文件含有 Donate 紀錄
- WHEN 管理員開啟 Donate 抽獎紀錄並查詢
- THEN 顯示所選篩選條件符合的頁面結果、目前頁碼與總筆數
- AND 查詢不修改紀錄、不付款、不進行抽獎或發送外部通知

#### Scenario: 已刪除活動仍保留歷史
- GIVEN Donate 活動已刪除，但其抽獎紀錄仍存在
- WHEN 管理員查詢該紀錄
- THEN 紀錄仍出現在結果中，活動欄顯示其 ID 與已刪除標示

### Requirement: Donate history is admin-only and data-minimized

Donate 紀錄 API MUST 要求管理員 session。API MUST 拒絕未授權與 OBS scoped session，並排除付款外部識別碼、私人捐款訊息、payment method 與 secret。

#### Scenario: 公開或 OBS session 無法讀取管理紀錄
- GIVEN 請求未帶管理員 session 或僅帶 OBS scoped session
- WHEN 請求 Donate 歷史
- THEN 回傳未授權／拒絕，且不回傳任何紀錄資料

#### Scenario: DTO 保護非必要欄位
- GIVEN 管理員查詢 Donate 歷史
- WHEN API 回傳紀錄
- THEN 僅包含列表與必要明細欄位，不包含 payment external ID、私人訊息或付款方式

### Requirement: Query states remain distinguishable

管理頁 MUST 分別呈現載入、錯誤、無資料、無符合條件及有資料狀態，並提供讀取失敗重試。導覽 MUST 清楚分開結果紀錄與系統設定，active route 僅標示一個項目。

#### Scenario: 讀取失敗不是空紀錄
- GIVEN Donate 歷史 API 回傳錯誤
- WHEN 頁面完成查詢
- THEN 顯示錯誤與重試，且不顯示「尚無紀錄」

#### Scenario: 篩選沒有結果
- GIVEN API 成功回傳空頁面
- WHEN 管理員查詢
- THEN 顯示無符合紀錄狀態與可調整條件提示
