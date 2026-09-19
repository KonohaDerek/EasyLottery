## 背景

EasyLottery 已有 YouTube 聊天擷取與多種 OBS 呈現，但缺乏可跨平台去重、可稽核身份與可重用互動回合。

## 目標

實作 Phase 1 多平台互動 MVP：統一觀眾身份、YouTube/Twitch connector、簽到、投票、答題、點數、排行榜、資格快照與 scoped OBS overlays。

## 範圍與非目標

僅支援 YouTube 與 Twitch；Discord、自助綁定碼與營運模板留至後續 change。不部署平台憑證或修改既有抽獎、Donate、OBS URL 相容性。

## 驗收與風險

同一 profile 跨平台僅能投一票，重送事件不重複計分，OAuth state 無效時不建立連線，OBS 重連先取得快照。以 feature flag 關閉 connector 和 overlays 即可回滾，不刪歷史資料。
