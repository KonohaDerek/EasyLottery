# Vue Phase 2：Admin Settings 遷移

## 背景

Phase 1 已讓 Vue 接管公開 About、Privacy Policy、Login，並保留 root 與未遷移功能給 Blazor。下一個低耦合邊界是 authenticated system settings；後端已有 resource-oriented settings API、ETag、secret redaction、Passkey admin API、backup API 與 OBS asset API。

## 目標

- 建立共用的 Vue authenticated admin layout 與 route guard。
- 遷移 payment、YouTube、admin access、audit、backup、OBS layout、asset、sound-cue、visual-style pages。
- 保持既有 session-token header、Passkey authorization、ETag concurrency 與 secret masking。
- 讓未遷移的 Blazor route 繼續可用，保留逐頁 rollback。

## 非目標

- 不遷移 lottery、activity、OBS realtime 或 SignalR pages。
- 不變更 production secrets、資料格式、Passkey protocol 或 deployment cutover。
- 不移除 EasyLotteryWasm、Blazorise 或現有 Blazor pages。

## 驗收標準

- 每個 Phase 2 Vue settings route 可用既有 API 載入並保存其設定或執行既有管理操作。
- 未登入直接進入 settings route 會導向 `/login`，且不發出 settings API request。
- ETag conflict、401/403 與 API error 會呈現可理解的錯誤，不吞掉失敗。
- Passkey list/add/remove、backup restore、asset upload/delete 保留既有 authorization。
- Vitest 覆蓋 route guard、settings client error/ETag 邊界；Playwright 覆蓋 authenticated route 與代表性 load/save flow。
- 非 Phase 2 路由仍由 Blazor 提供。

## 測試策略

- Vitest：route guard、API error/ETag、settings payload normalization。
- Playwright：未登入 guard、admin settings shell、visual style save、Passkey page、既有 Blazor regression。
- .NET：既有 Release build/tests，確認 backend contract 未變。
- OpenSpec strict validation。

## 回滾策略

只移除 Phase 2 Vue route mappings 或在 router 中停用 `requiresAuth` settings routes，即可把這些 URL 交還既有 Blazor fallback；backend API、資料與 Passkey state 不變。

## 觀測性

- 未登入 settings navigation 不得呼叫 `/api/settings/*`、`/api/admin/passkeys` 或 `/api/obs-assets`。
- save/restore/upload 錯誤保留 HTTP status 與 server error message 給使用者，但不輸出 secret/token。
- ETag conflict 顯示重新載入提示，避免覆寫其他 admin 的更新。
