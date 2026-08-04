# Template Management Dialog

## ADDED Requirements

### Requirement: Same-page template editing

戳戳樂與轉盤管理頁 SHALL 在同一頁以 Dialog 完成新增與編輯，不要求使用者離開列表頁。

#### Scenario: Create a template

- **WHEN** 使用者按下「新增模板」
- **THEN** 系統顯示空白模板 Dialog
- **AND** 儲存成功後關閉 Dialog 並更新列表

#### Scenario: Cancel template editing

- **WHEN** 使用者修改欄位後取消或關閉 Dialog
- **THEN** 列表與持久化資料維持原值

### Requirement: Consistent editor width

模板 Dialog SHALL 在桌面使用至少 75% viewport width，並在手機畫面保留安全邊距。

#### Scenario: Desktop editor

- **WHEN** viewport 寬度大於行動裝置斷點
- **THEN** Dialog 寬度為 75vw

### Requirement: Direct OBS operations

已發布模板 SHALL 在列表提供正式 OBS 與測試 OBS 操作。

#### Scenario: Open OBS

- **WHEN** 使用者按下正式 OBS
- **THEN** 新分頁 URL 包含有效 session token

#### Scenario: Open test OBS

- **WHEN** 使用者按下測試 OBS
- **THEN** 新分頁 URL 包含有效 session token 與 `controls=1`

### Requirement: Preserve template capabilities

管理頁 SHALL 保留模板複製、匯出、匯入以及內建模板刪除保護。

#### Scenario: Delete a custom template

- **WHEN** 使用者刪除非內建模板
- **THEN** 系統先顯示確認提示
- **AND** 確認後刪除並更新列表

#### Scenario: Built-in template

- **WHEN** 列表顯示內建模板
- **THEN** 系統不提供刪除操作
