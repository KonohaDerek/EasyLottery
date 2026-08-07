# YAML 一次性匯入 SQLite

## Why

切換到 SQLite 後，需要將既有分拆 YAML 的活動、模板與抽獎結果安全搬入新資料庫。

## Goals

- 提供受保護的一次性匯入 API。
- 回傳匯入筆數，方便驗證資料一致性。
- 僅允許 SQLite provider 執行匯入，避免誤覆寫 YAML。

