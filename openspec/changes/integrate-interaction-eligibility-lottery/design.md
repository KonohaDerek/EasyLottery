# 設計

## API 與資料流

1. 核心抽獎頁列出已存在的互動回合，選取 round 後呼叫現有 eligibility preview。
2. 預覽至少顯示 round 狀態/名稱、候選人數、已匯入快照狀態、預計新增/重複/衝突數；只有管理員可讀取 Profile ID 與關聯身份資料。
3. 管理員明確確認後呼叫現有 eligibility import。以回傳的穩定 snapshot ID 和 Profile ID 建立帶來源資料的 participant，並在單一狀態轉換中合併名單。
4. Participant provenance 為 optional typed 欄位：round ID、snapshot ID、AudienceProfile ID；displayName 和外部身份只複製抽獎當下必要值。相同 Profile ID 對同一抽獎 pool 僅建立一個 eligibility entry；名稱相同但無穩定身份連結的手動列保留並提示可能重複，不自動合併。
5. Lock 建立版本化且不可變的 pool snapshot（參加者、獎項、來源 IDs、鎖定時間及 hash），保存在 `sessionStorage`，恢復時重新驗證 schema/hash。所有會改候選者或獎項的操作與聊天匯入在 lock 期間停用。解鎖需明確操作並清除 lock，不能修改既有 draw/history。
6. Draw record participant snapshot 增加可空 source round/snapshot/profile ID 欄位；現有 copy/validate/idempotency 比較也包含這些欄位。API history 授權不變，公開 overlay DTO 不增加這些欄位。
7. Draw request/record 增加可空 `poolSnapshotVersion`（`lockHash` + `lockedAtUtc`），識別抽獎所用的鎖定名單版本。鎖定時每個 prepared/selected/result 狀態都帶同一版本，經 localStorage 重載、對帳與重試保留；未鎖定與舊紀錄為 null/省略。API 驗證（16 位 hex hash、非預設時間）、repository 複製並納入同一 drawId 的冪等比較（hash 與時間不同或有無不同即 409）。只出現在需管理員的 lottery-draw-records summary/detail。

## 手動名單衝突

- 不用 displayName 相等去推斷身份。
- 身份鍵與 domain `PlatformIdentity.EventScopeKey` 一致：platform + channelScope + accountId。Eligibility 投影（admin-only）帶出 `channelScope`；前端 `ParticipantIdentity.channelScope` 為 optional，匯入參加者與鎖定名單保留此欄位（舊資料缺少時仍可讀取，鎖定 hash 僅在有值時納入）。
- 只有 platform、channelScope、accountId 三者完全相同時，才可把手動 participant 連結到匯入 Profile ID：保留原 participant / lotteryGroupId 並補上 provenance；不得增加新的抽獎票。
- 同 platform + accountId 但手動身份或匯入身份缺少 channelScope、或 scope 不同、或已被其他 profile/先前條目佔用時，不自動連結；分類為 `identity-conflict`，以獨立資格加入並列出相關 participant 供主持人確認。
- 若只同名但身份無法解析，保留獨立手動列，預覽提示名稱相同但未確認身份；主持人需自行確認。此行為確定且可測試，避免靜默吞列。
- Profile ID 的 merge/unlink 只影響未來 preview；已鎖定 pool、pending draw 與歷史記錄保留匯入時的原 Profile ID。

## 併發與失敗

- Import 按鈕 pending 時不可重複提交；重試同一 snapshot 仍為冪等。
- 失效/非支援狀態的回合不匯入；空結果不更動現有名單。
- preview/import/read 失敗時保留現有名單與 pool lock 狀態並顯示重試；不可將錯誤當成空名單。
- Locked snapshot 的結構、版本或 hash 驗證失敗時禁止抽獎，提供清除損壞 lock 的明確復原操作。

## 測試策略

- .NET: preview/import 的 round 不存在、空 round、重複匯入、profile IDs 與來源快照穩定、身份 merge/unlink 不回寫既有來源 snapshot、同 accountId 不同 channelScope 保持不同 profile/identity、pool version 寫入/回傳/冪等衝突/舊紀錄 null。
- Vitest: profile 去重、手動 identity 衝突（精確 scope 連結；缺少或不同 scope 不連結並回報）、同名不同身份、lock/unlock、snapshot hash/schema/重載、channelScope 經鎖定保留、pool version 經 prepared/result/重載/對帳保留、錯誤時不變更名單。
- Playwright: 管理員選 round、預覽差異、明確匯入、冪等重試、lock 後禁止互動變更、重載恢復、draw payload 與 history 帶 lock version、未鎖定抽獎不帶 version、解鎖後更新，以及既有手動抽獎路徑。
- 既有核心抽獎、歷史、互動 round .NET 與 Vue 測試維持通過。

## 回滾

新增後端歷史欄位 nullable 且忽略舊客戶端遺漏欄位。回滾 UI/API 時保留已保存歷史來源欄位；移除暫存 lock 只清同 tab 的 sessionStorage key。不得刪除既有抽獎歷史。
