## 架構

Connector 將平台事件正規化為 `InteractionEvent`；純 domain engine 依 `AudienceProfileId` 作出 immutable `InteractionDecision`；repository 以外部事件 key 冪等保存；API 將 redacted snapshot 以 scoped SignalR 提供給 host 與 OBS。

## 安全

所有 API key、OAuth token 與 webhook secret 僅於 server storage 保存及遮罩回傳。OBS 使用 read-only scoped token，不可開始、暫停或結算互動回合。
