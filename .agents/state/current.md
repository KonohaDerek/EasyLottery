# Current work state

- Task ID: continuous-execution-policy-2026-09-20
- Status: Done
- Scope: 將已核准 Native 任務的持續執行、進度回報與回合恢復規則同步至 repository workflow；未修改業務程式碼。
- OpenSpec change: not required (low-risk repository configuration)
- Continuation policy: 在完成驗證、推送與建立 Ready PR 前，進度詢問不得暫停已核准 Native 工作；新回合先讀取本檔並續作，除非使用者已變更任務或存在已記錄的允許中斷。
- Notes: Jev is enabled through TypeSafe for structured workflow-routing decisions. Workflow loading is enforced through `.agent/INSTRUCTIONS.md`.
- Updated: 2026-09-20
