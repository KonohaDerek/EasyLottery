## Why

目前 OBS 加班頁同時顯示主事件、狀態徽章、控制區與多個資訊卡，直播時資訊量過多，會干擾觀眾看台，也讓操作焦點不夠單純。使用者希望 OBS 只保留最重要的直播資訊，並在收到 donate 時自動延長倒數，不再讓設定頁參與這段即時流程。

## What Changes

- 簡化 OBS 加班頁版面，只保留一個倒數文字、目前 donate/Support 事件的單行資訊，以及必要的最小狀態。
- 移除 OBS 頁上的開始/結束操作區、預覽式多卡片內容與其他容易分心的資訊區塊。
- donate / SuperChat 事件在 OBS 上只顯示一行，並在指定秒數後自動消失。
- donation 觸發的加班延長仍維持現有規則計算，但呈現方式改為更低干擾的直播提示。
- 加班開始由 OBS 頁上的開始按鈕控制，設定頁只保留預計關台時間與規則配置。
- 加班事件本身攜帶最新的結束時間，OBS 只讀事件流來決定倒數。
- 加班開始與結束狀態由 OBS 頁主導顯示與更新，設定頁只保留配置，不再承擔即時展示責任。

## Capabilities

### New Capabilities
- `overtime-obs-minimal-overlay`: 定義 OBS 加班頁的極簡直播呈現、倒數進度條、donate 單行訊息與自動消失行為。

### Modified Capabilities
- `overtime-overlay`: 調整既有加班台能力的要求，將 OBS 呈現重心從多資訊卡改為極簡倒數與事件提示。

## Impact

- `src/EasyLotteryWasm/Pages/Overtime/OvertimeOverlay.razor`
- `src/EasyLotteryWasm/Pages/Config/OvertimeOverlay.razor`
- `src/EasyLotteryDomain/Services/OvertimeSessionTimeCalculator.cs`
- `src/EasyLotteryDomain/Models/Config/OvertimeOverlaySettings.cs`
- `tests/EasyLotteryDomainTests/Services/*`
- `openspec/changes/simplify-overtime-obs-overlay/specs/*`
