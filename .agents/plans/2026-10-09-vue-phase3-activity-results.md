# Vue Phase 3 — Activity Results slice

需求來源：GitHub #268，前一階段 PR #274 已合併到 `main`。

## 範圍

由主 Agent 在 `feat/vue-frontend-phase3` 實作 `ActivityResults` 瀏覽頁，沿用 Vue shared API client 與既有 GET endpoint。讀取與篩選行為先由單元測試規格化，再新增 view、admin route、host fallback 和 Blazor full-page handoff。

Read-only inventory 由 gpt-6-luna/high 子 Agent 完成；其不修改檔案。主 Agent 負責 scope、OpenSpec、程式整合、驗證、push 與 Ready PR。

## 非目標與風險

- 不讀取 Donate payment summary、settings YAML，不改 API/Domain，不觸發任何抽獎或通知。
- 不遷移編輯／管理流程、OBS、SignalR，不刪除 Blazor/Blazorise，不部署 production。
- 風險集中在 admin token header、精確 fallback、瀏覽器本地日期及 CSV 格式；原 Blazor 頁保留供快速回滾。

## 驗證順序

1. Vitest 驗證篩選、排序、CSV header/BOM/CRLF/引號跳脫。
2. Vue typecheck、unit tests、production build。
3. API-hosted Playwright：未登入 redirect、不送 API、Blazor 導覽交接、admin header、錯誤重試、篩選／明細／下載。
4. Dev Container 內執行 Domain/API regression、OpenSpec strict validation、`git diff --check`。
5. 主 Agent 檢視完整 diff 與回滾路由；提交並 push，建立 Ready for review PR，引用 #252/#268。

Dev Container 透過 repo 自帶 Dockerfile 建置。此工作不使用 production credentials；容器中的測試 storage 必須在 `/tmp` 隔離。
