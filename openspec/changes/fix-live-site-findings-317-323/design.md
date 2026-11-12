## Context

依已發布 stable v0.0.7，Claude 規劃、Codex 實作。實際規劃見 .agents/plans/2026-11-12-live-fixes.md；原 E2E 資料完整保留。

## Goals / Non-Goals

修正 #317–#323 並用無預期失敗的本機回歸驗收。保留 legacy、認證、安全界線及透明 OBS。不得部署或改正式設定，不新增 quiet query 或 interaction OBS 功能。

## Decisions

- 具體 API/hub catch-all 回 problem404，Vue 非檔案未知導覽回 HTTP404。Blazor 深層路由明確保留。
- settings 只對 Sec-Fetch 文件導覽轉址，程式客戶端保留 YAML/權限。GET/HEAD 一致。
- 未知 email options 以持久 random seed/HKDF 產生穩定 ID；註冊訊息先驗來源。真/假 login 都執行相同 gated 讀寫，假帳號不覆寫現有 challenge。錯誤 verify 統一，未知 email 不消耗他人 challenge。
- Login client validation 關聯 input 與錯誤；WebAuthn 取消訊息不暴露瀏覽器特定帳號資訊。
- OBS 致命錯誤在無內容時可見，保留正常閒置與暫時 polling 失敗時的既有內容。

## Risks / Trade-offs

不宣稱分散式環境嚴格 constant-time。正式 allowlist 設定未查驗。未知頁由200變404，OBS 無內容時顯示錯誤。先前未公開的註冊 state seed 為新欄位，不記錄到 log。

## Migration Plan

無 schema migration。回滾 PR；舊版會忽略 additive seed 欄位，若舊版寫回移除它，之後新版會重新產生。

## Verification

完整 Vue tests/typecheck/build、API/Domain tests、WebAuthn 與本機 browser E2E、OpenSpec、diff check、同一 SHA 的真實 Codex/Claude 獨立審查及 CI。最多五個候選審查輪；證據外置，任何候選變更都重新審查。正式驗收未授權，不移除 live 預期失敗標記。
