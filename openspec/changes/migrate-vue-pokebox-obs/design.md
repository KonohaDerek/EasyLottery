# 設計

## 路由與呈現

- Vue route 為 `/obs/pokebox/:publicId`，不套用管理員 shell；API host 只為該 GUID route 指向 Vue HTML。
- Blazor component 保留 `/legacy/obs/pokebox/{PublicId:guid}`，作為獨立回退入口。
- `controls=1` 控制輔助操作區是否顯示，不代表授權；所有受保護 API 與 hub Join 繼續由 OBS session token 的 resource/scope 驗證。
- 空閒、載入或沒有可顯示結果時維持透明背景；保留模板格數/順序、圖像、revealed color、背景、動畫與 reduced-motion/低品質選項。

## API、token 與 SignalR

- 啟動時沿用共用 `bootstrapObsSessionToken()`；從 URL 取得 token 後寫入既有儲存/in-memory fallback，再用 `history.replaceState` 清除 query/fragment 憑證。
- 以既有 `/api/poke-templates/public/{publicId}` 讀取模板和持久揭露格；以 `/api/live-draw/pokebox/{publicId}` 讀取即時狀態。
- 使用 `/hubs/live-draw`、`JoinSession("pokebox", publicId, token)`、`LiveDrawStateChanged`。忽略舊 revision；重連時重新加入群組並取得 REST snapshot。不得在 Vue 選擇抽獎結果。
- 手動/隨機戳格、重設與記錄分別呼叫既有 `/api/live-draw/pokebox/.../poke`、`reset`、`/api/poke-templates/public/.../record-result`。結果通知沿用現有 endpoint。
- 音效沿用舊 overlay 的全域 Sound Cue preset；模板上的 `PokeSoundUrl`/`OpenSoundUrl` 欄位不在這次改變其既有使用語意。

## 驗證、觀測與回復

- Vitest 覆蓋 phase mapping、revision 順序與 session/URL 的必要邊界。
- Playwright 驗證 token 清理、read/control token、雙頁同步、手動/隨機操作、reset、record-result、重連/快照與 legacy alias；在 1280×720、1920×1080 驗證版面。
- 控制模式呈現連線/錯誤狀態；Browser Source 錯誤可見且不輸出 token 到 URL/console。
- 回復只切回 legacy Blazor route，模板資料和 Live Draw API 不變；無法用 Chromium 自動測試取代原生 OBS Browser Source smoke。
