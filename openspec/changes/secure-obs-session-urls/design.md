## Context

EasyLottery 以同源 Blazor WASM 與 ASP.NET Core API 提供管理及 OBS overlay。原本所有設定 API 都檢查一個由部署者手動提供的 `Settings:AdminToken`；OBS 頁在新視窗開啟時不會自然攜帶該值，造成 401 與人工設定成本。Donate 活動的 OBS URL 使用遞增整數 ID，也容易被猜測。

## Goals / Non-Goals

**Goals:**

- 在不需要部署設定共用密碼的前提下，讓 WASM 自動取得短期 Session JWT。
- 讓設定 API 及 OBS 頁驗證同一枚 JWT，並讓產生的 OBS URL 自動攜帶該 JWT。
- 以 GUID 作為 Donate OBS URL 的公開活動識別碼，同時可讀取既有整數 URL。
- 維持 API 與 WASM 的同源部署模式，避免導入外部身分提供者。

**Non-Goals:**

- 不提供跨裝置登入、使用者帳號、角色權限或可撤銷的長期憑證。
- 不改變 Donate 抽獎邏輯、支付回呼的驗證機制或既有 YAML 格式以外的資料遷移。
- 不保證 API 重啟後舊 OBS URL 繼續有效。

## Decisions

### 使用程序記憶體中的隨機簽章金鑰與 12 小時 JWT

API 啟動時以加密亂數產生對稱式簽章金鑰，由 singleton token service 發行及驗證 JWT。這移除了部署設定中的固定密碼，且 API 重啟會自然使舊憑證失效。選擇 JWT 而不是伺服器端 session store，因為 OBS URL 可在新分頁中以 query string 自足驗證，不需新增持久化狀態。

### API 從標頭或 query string 驗證 Session JWT

一般 WASM API 請求使用 `X-EasyLottery-Session-Token` 標頭，OBS 開啟 URL 則使用 `sessionToken` query string。共用的 access service 統一解析與驗證，避免每個 endpoint 自行實作授權。query string 僅用於需直接貼給 OBS 的 URL；它可能出現在瀏覽器紀錄中，因此憑證有期限且不跨 API 重啟有效。

### Donate 活動使用持久化 GUID 公開 ID

`PublicId` 儲存在活動模型與 YAML projection；新活動立即產生 GUID，讀取舊資料時正規化補齊。公開 OBS URL 以 GUID 路由查詢；整數路由僅為舊連結相容性保留。這比雜湊數字 ID 更容易驗證，也不會暴露遞增資料量。

### WASM 在 session storage 管理當前憑證

JavaScript storage 層啟動時會接受 OBS URL 的 `sessionToken`、移除網址列中的該參數，並將 token 放在 `sessionStorage`；若不存在則呼叫同源 `/api/session-token`。這讓一般管理頁與 OBS 新分頁共用流程，而不會將憑證持久保存於 localStorage。

## Risks / Trade-offs

- [OBS URL 被複製或瀏覽器歷程保存] → JWT 有 12 小時效期，載入後會從網址列移除；使用者仍須妥善保管分享的 URL。
- [API 重啟使直播中的 overlay 失效] → API 重啟本就會中斷 overlay；重新開啟管理頁取得新的 URL 即可恢復。
- [舊 YAML 沒有 PublicId] → repository 與規則層在讀取/儲存時補上 GUID；整數路由保持相容。
- [同源取得 token endpoint 可被其他瀏覽器呼叫] → 本設計處理的是不必人工 token 與 URL 不可猜測，不提供完整帳號驗證；對外公開部署需再導入登入／網路層限制。

## Migration Plan

1. 部署 API 與 WASM 變更，移除 `Settings:AdminToken` 設定。
2. 啟動後 API 自動生成簽章金鑰；使用者重新開啟管理頁即可取得新 session。
3. 舊 YAML 活動於下一次讀取或儲存時補齊 `PublicId`。
4. 書籤與舊 OBS 整數 URL 暫時仍可使用；新複製的 URL 一律採 GUID。
5. 如需 rollback，回退程式即可恢復舊端點；新 YAML 欄位會被舊反序列化器忽略。

## Open Questions

- 未來若系統提供帳號登入，Session JWT 應改由登入身分簽發並加入角色／撤銷策略。
