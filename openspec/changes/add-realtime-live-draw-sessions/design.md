# 設計

## 工作階段識別

戳戳樂與轉盤模板各自新增持久化 `PublicId`。管理頁使用 `/obs/pokebox/{publicId}` 與 `/obs/roulette/{publicId}`；舊的整數路由繼續可讀取，避免既有書籤立即失效。

## 伺服器狀態機

`LiveDrawSessionService` 以抽獎類型與 `PublicId` 作為鍵保存最新快照。命令進入後依序發布 `CountingDown`、`Animating`、`ShowingResult` 狀態，並以遞增 revision 讓前端避免重複播放。

戳戳樂的格子與轉盤的項目只在伺服器決定一次。持久化操作使用全域命令鎖序列化，避免 YAML 讀寫互相覆蓋。

## 傳輸

- REST 取得快照及提交戳戳、轉盤、重設命令。
- SignalR Hub 依 `kind/publicId` 群組廣播 `LiveDrawStateChanged`。
- REST 與 Hub 都沿用 OBS session token 驗證。

## 前端同步

`LiveDrawSessionClient` 建立 SignalR 連線、加入工作階段群組並訂閱狀態。自動重連成功後重新加入群組，再以 REST 讀取最新快照。控制按鈕只送出命令，不在瀏覽器自行抽獎。

OBS 元件同時支援 GUID 與舊整數路由。GUID 路由使用即時工作階段；舊路由維持相容載入。
