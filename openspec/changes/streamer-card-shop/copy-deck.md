# Streamer Card Shop：zh-TW 文案表（Copy Deck）

版本：v0.1 草稿（2026-11-14）
依據：`openspec/changes/streamer-card-shop/design.md` D2、D5、D8、D10、D11 與 specs/
語系：zh-TW，全形標點；品牌與技術名詞（EasyLottery、Card Hub、Passkey、OBS、Donate、email）沿用既有介面寫法，不翻譯。

## 撰寫規則

- 語氣比照既有管理介面：短句、直述，錯誤訊息用「……失敗。」或「……，請稍後再試。」，不用驚嘆號。
- 觀眾端一律稱「你」；實況主端也稱「你」（與既有登入頁一致）。
- `{xxx}` 為佔位符，開發時以 i18n 參數帶入，請勿翻譯。日期一律由前端依 `zh-TW` 格式化（例：2026/11/14 21:30）。
- 標記 **[需法務確認]** 的字串在上線前必須經法務或負責人確認，開發可先使用草稿。
- 數值（10 分鐘、60 秒、5 次、24 小時、30 天、90 天）來自設計文件；其中兌換碼效期 90 天仍列在「Open decisions」，因此文案用 `{expiryDays}` 帶入，預設 90。

### 佔位符一覽

| 佔位符 | 說明 |
| --- | --- |
| `{streamerName}` | 實況主（tenant）顯示名稱 |
| `{viewerEmail}` / `{streamerEmail}` | 收件 email，只出現在寄給本人的信件 |
| `{loginCode}` | 6 位數登入驗證碼 |
| `{redeemCode}` | 兌換碼，格式 `XXXX-XXXX-XXXX-XXXX-XXXX-XXXX` |
| `{inviteUrl}` / `{inviteExpiresAt}` | 邀請連結與到期時間 |
| `{packName}` | 卡包名稱 |
| `{expiresAt}` / `{expiryDays}` | 兌換碼到期時間與天數（預設 90） |
| `{seconds}` | 倒數秒數 |
| `{minutes}` | 稍後可重試的分鐘數 |
| `{deletionDate}` | 實況主資料刪除日 |
| `{appStoreUrl}` / `{googlePlayUrl}` | 商店連結（待上架後填入） |
| `{supportEmail}` | Card Hub 客服信箱（待營運議題決定） |
| `{termsUrl}` / `{privacyUrl}` | 服務條款與隱私權政策連結 |

---

## 1. 實況主邀請信與邀請頁

### 1.1 邀請信（Hub 寄出，收件人為受邀實況主）

| Key | 文案 |
| --- | --- |
| `hub.invite.email.subject` | 你受邀加入 EasyLottery Card Hub 測試 |
| `hub.invite.email.greeting` | 你好， |
| `hub.invite.email.body.intro` | 我們邀請你以實況主身分加入 Card Hub 封閉測試。加入後，你可以把自己 EasyLottery 網站上的卡牌與卡包發布到 Card Hub，讓觀眾在 App 中收藏與交換。 |
| `hub.invite.email.body.action` | 請使用下方連結建立實況主帳號，並以此 email 與 Passkey 完成註冊： |
| `hub.invite.email.cta` | 接受邀請 |
| `hub.invite.email.body.expiry` | 此邀請只能使用一次，將於 {inviteExpiresAt} 失效，且只適用於 {streamerEmail}。 |
| `hub.invite.email.body.safety` | 如果你沒有申請或不認識此邀請，請直接忽略這封信，不需要任何動作。 |
| `hub.invite.email.signature` | EasyLottery Card Hub |
| `hub.invite.email.footer` | 這是系統自動寄出的信件，請勿直接回覆。如有問題請來信 {supportEmail}。 |

### 1.2 邀請頁（點開連結後）

| Key | 文案 |
| --- | --- |
| `hub.invite.page.title` | 加入 Card Hub |
| `hub.invite.page.lead` | 你正在以 {streamerEmail} 建立 Card Hub 實況主帳號。 |
| `hub.invite.page.steps` | 完成註冊後，請到你的 EasyLottery 網站「Card Hub 連結」頁面填入站台憑證。 |
| `hub.invite.page.passkeyHint` | Passkey 私鑰會留在你的裝置或密碼管理器，Card Hub 不會取得。 |
| `hub.invite.page.terms` | 我已閱讀並同意〈實況主服務條款〉與〈隱私權政策〉。 [需法務確認] |
| `hub.invite.page.submit` | 以 Passkey 建立帳號 |
| `hub.invite.page.submitting` | 建立中… |
| `hub.invite.page.success` | 帳號已建立。下一步請登記你的 EasyLottery 網站並取得站台憑證。 |
| `hub.invite.page.error.expired` | 此邀請已過期，請聯絡邀請你的管理員重新寄送。 |
| `hub.invite.page.error.used` | 此邀請已經使用過。若你已有帳號，請直接以 Passkey 登入。 |
| `hub.invite.page.error.invalid` | 邀請連結無效，請確認連結完整，或聯絡邀請你的管理員。 |
| `hub.invite.page.error.emailMismatch` | 此邀請綁定的 email 與目前登入的帳號不同，請改用受邀 email。 |
| `hub.invite.page.error.passkeyCancelled` | Passkey 註冊已取消。 |
| `hub.invite.page.error.passkeyFailed` | Passkey 註冊失敗，請稍後再試。 |
| `hub.invite.page.closedBeta` | Card Hub 目前採邀請制，暫不開放自行申請。 |

---

## 2. 觀眾登入驗證碼信

| Key | 文案 |
| --- | --- |
| `viewer.loginCode.email.subject` | 你的 Card Hub 登入驗證碼：{loginCode} |
| `viewer.loginCode.email.subject.streamer` | {streamerName} 卡牌收藏：你的登入驗證碼 {loginCode} |
| `viewer.loginCode.email.body.intro` | 你正在登入 Card Hub。 |
| `viewer.loginCode.email.body.intro.streamer` | 你正在從 {streamerName} 的卡牌活動登入 Card Hub。 |
| `viewer.loginCode.email.body.code` | 你的驗證碼是：{loginCode} |
| `viewer.loginCode.email.body.expiry` | 驗證碼 10 分鐘內有效，只能使用一次。 |
| `viewer.loginCode.email.body.safety` | 如果不是你本人操作請忽略這封信，你的帳號不會有任何變更。請勿將驗證碼提供給任何人，包括實況主或客服人員。 |
| `viewer.loginCode.email.signature` | EasyLottery Card Hub |
| `viewer.loginCode.email.footer` | 這是系統自動寄出的信件，請勿直接回覆。 |

備註：`*.streamer` 變體在登入由實況主情境（例：兌換信連結、實況主頁）開始時使用，其餘情況使用一般版。主旨把驗證碼放在最後，方便手機通知直接看到。

---

## 3. 觀眾註冊／登入畫面（App）

### 3.1 輸入 email

| Key | 文案 |
| --- | --- |
| `viewer.auth.title` | 登入 Card Hub |
| `viewer.auth.title.streamer` | 登入以收藏 {streamerName} 的卡牌 |
| `viewer.auth.lead` | 一個帳號就能收藏多位實況主的卡牌。輸入 email，我們會寄一組 6 位數驗證碼給你。 |
| `viewer.auth.email.label` | email |
| `viewer.auth.email.placeholder` | name@example.com |
| `viewer.auth.email.invalid` | 請輸入有效的 email。 |
| `viewer.auth.sendCode` | 寄送驗證碼 |
| `viewer.auth.sending` | 寄送中… |
| `viewer.auth.emailPrivacy` | 你的 email 只用於登入與寄送兌換碼，不會顯示給其他觀眾。 |

### 3.2 首次註冊：年齡與條款

| Key | 文案 |
| --- | --- |
| `viewer.signup.title` | 建立帳號 |
| `viewer.signup.age.label` | 你的年齡 |
| `viewer.signup.age.helper` | 我們只記錄年齡區間，不會要求生日。 |
| `viewer.signup.age.option.13_17` | 13–17 歲 |
| `viewer.signup.age.option.18_plus` | 18 歲以上 |
| `viewer.signup.age.option.under13` | 未滿 13 歲 |
| `viewer.signup.age.required` | 請選擇年齡區間。 |
| `viewer.signup.age.minorNotice` | 未滿 18 歲的使用者，請在法定代理人（例如父母）閱讀並同意條款後再使用。 [需法務確認] |
| `viewer.signup.under13.title` | 目前無法建立帳號 |
| `viewer.signup.under13.body` | 謝謝你的興趣。Card Hub 目前只開放 13 歲以上的使用者註冊，這次不會建立帳號，也不會保存你剛輸入的 email。 [需法務確認] |
| `viewer.signup.under13.action` | 返回 |
| `viewer.signup.terms.checkbox` | 我已閱讀並同意〈服務條款〉與〈隱私權政策〉。 [需法務確認] |
| `viewer.signup.terms.link.terms` | 服務條款 |
| `viewer.signup.terms.link.privacy` | 隱私權政策 |
| `viewer.signup.terms.required` | 請先閱讀並同意服務條款與隱私權政策。 |
| `viewer.signup.terms.updated` | 服務條款已更新，請閱讀並同意後繼續使用。 [需法務確認] |
| `viewer.signup.submit` | 建立帳號 |

備註：未滿 13 歲的提示不可引導使用者「改選其他年齡」；畫面只提供「返回」。

### 3.3 輸入驗證碼

| Key | 文案 |
| --- | --- |
| `viewer.code.title` | 輸入驗證碼 |
| `viewer.code.lead` | 驗證碼已寄到 {viewerEmail}，10 分鐘內有效。 |
| `viewer.code.input.label` | 6 位數驗證碼 |
| `viewer.code.submit` | 登入 |
| `viewer.code.verifying` | 驗證中… |
| `viewer.code.changeEmail` | 更換 email |
| `viewer.code.notReceived` | 沒收到信？請檢查垃圾郵件或促銷郵件匣。 |
| `viewer.code.resend` | 重新寄送 |
| `viewer.code.resend.countdown` | {seconds} 秒後可重新寄送 |
| `viewer.code.resend.sent` | 已重新寄送驗證碼，先前的驗證碼將失效。 |
| `viewer.code.resend.tooSoon` | 寄送太頻繁，請等 {seconds} 秒後再試。 |
| `viewer.code.error.wrong` | 驗證碼不正確，請再確認一次。 |
| `viewer.code.error.wrongWithRemaining` | 驗證碼不正確，還可以再試 {remaining} 次。 |
| `viewer.code.error.tooManyAttempts` | 錯誤次數過多，這組驗證碼已失效。請重新寄送驗證碼。 |
| `viewer.code.error.rateLimited` | 嘗試次數過多，請在 {minutes} 分鐘後再試。 |
| `viewer.code.error.expired` | 驗證碼已過期，請重新寄送。 |
| `viewer.code.error.network` | 無法連線到 Card Hub，請檢查網路後重試。 |

---

## 4. 實況主網站「Card Hub 連結」設定頁（EasyLottery 管理介面）

### 4.1 標題與欄位

| Key | 文案 |
| --- | --- |
| `hub.link.title` | Card Hub 連結 |
| `hub.link.lead` | 將這個 EasyLottery 網站連結到 Card Hub 後，才能發布卡牌、卡包並讓 Donate 抽卡寄出兌換碼。 |
| `hub.link.field.hubUrl.label` | Card Hub 網址 |
| `hub.link.field.hubUrl.helper` | 通常不需要修改，請使用 Card Hub 提供的網址。 |
| `hub.link.field.clientId.label` | 站台 Client ID |
| `hub.link.field.clientId.helper` | 在 Card Hub 實況主後台的「站台」頁面可以找到。 |
| `hub.link.field.clientSecret.label` | 站台 Client Secret |
| `hub.link.field.clientSecret.helper` | 密鑰會加密保存在此網站，儲存後不會再顯示於瀏覽器。 |
| `hub.link.field.clientSecret.placeholderSaved` | 已設定時可留白保留伺服器值 |
| `hub.link.field.tenant.label` | 實況主名稱 |
| `hub.link.field.lastPublish.label` | 最後發布時間 |
| `hub.link.field.lastCheck.label` | 最後檢查時間 |
| `hub.link.save` | 儲存並驗證 |
| `hub.link.saving` | 驗證中… |
| `hub.link.testConnection` | 測試連線 |
| `hub.link.testing` | 測試中… |
| `hub.link.unlink` | 解除連結 |
| `hub.link.unlink.confirm` | 解除後此網站將無法發布卡牌或觸發 Donate 抽卡，已發出的卡牌不受影響。確定要解除嗎？ |
| `hub.link.shopDisabled` | 卡牌商店目前未啟用（Shop:Enabled 為 false），連結設定仍可先完成。 |

### 4.2 四種狀態

每個狀態一行說明 + 一個下一步動作。

| Key | 文案 |
| --- | --- |
| `hub.link.status.notLinked` | 未連結 |
| `hub.link.status.notLinked.desc` | 此網站尚未設定 Card Hub 憑證。 |
| `hub.link.status.notLinked.action` | 請在 Card Hub 取得 Client ID 與 Secret 後填入下方欄位。 |
| `hub.link.status.linked` | 已連結 |
| `hub.link.status.linked.desc` | 已連結到 Card Hub 的「{streamerName}」，可以發布卡牌與卡包。 |
| `hub.link.status.linked.action` | 需要時可按「測試連線」確認狀態。 |
| `hub.link.status.invalid` | 憑證失效 |
| `hub.link.status.invalid.desc` | Card Hub 拒絕了此網站的憑證，可能已被撤銷、輪替逾期，或實況主帳號已停用。 |
| `hub.link.status.invalid.action` | 請到 Card Hub 產生新的 Secret，再貼到下方並儲存。 |
| `hub.link.status.unreachable` | Hub 無法連線 |
| `hub.link.status.unreachable.desc` | 目前連不上 Card Hub，這是網路或服務問題，不代表憑證有誤。 |
| `hub.link.status.unreachable.action` | 請稍後再按「測試連線」；期間的 Donate 抽卡會排隊，恢復後自動重送。 |

### 4.3 測試連線 Toast

| Key | 文案 |
| --- | --- |
| `hub.link.toast.testSuccess` | 連線成功，憑證有效。 |
| `hub.link.toast.testInvalid` | 連線失敗：憑證無效或已撤銷。 |
| `hub.link.toast.testUnreachable` | 連線失敗：無法連到 Card Hub，請稍後再試。 |
| `hub.link.toast.testTenantDisabled` | 連線成功，但此實況主帳號目前已停用，暫時無法發布或抽卡。 |
| `hub.link.toast.saved` | Card Hub 連結已儲存並驗證。 |
| `hub.link.toast.saveFailed` | Card Hub 連結儲存失敗。 |
| `hub.link.toast.unlinked` | 已解除 Card Hub 連結。 |

### 4.4 Secret 只顯示一次（Card Hub 實況主後台）

| Key | 文案 |
| --- | --- |
| `hub.site.secret.title` | 新的 Client Secret |
| `hub.site.secret.warning` | 這組 Secret 只會顯示這一次。請立即複製並貼到你的 EasyLottery「Card Hub 連結」頁面，關閉後就無法再查看。 |
| `hub.site.secret.copy` | 複製 Secret |
| `hub.site.secret.copied` | 已複製 Secret。 |
| `hub.site.secret.confirmClose` | 我已複製並保存 |
| `hub.site.secret.noShare` | 請勿把 Secret 貼在聊天室、截圖或分享給他人。 |

### 4.5 輪替與撤銷說明

| Key | 文案 |
| --- | --- |
| `hub.site.rotate.action` | 輪替 Secret |
| `hub.site.rotate.explain` | 輪替會產生新的 Secret。舊的 Secret 會繼續有效 24 小時，讓你有時間更新網站設定，之後自動失效。 |
| `hub.site.rotate.confirm` | 確定要產生新的 Secret 嗎？舊的 Secret 將在 24 小時後失效。 |
| `hub.site.rotate.done` | 已產生新的 Secret，舊的 Secret 將於 {expiresAt} 失效。 |
| `hub.site.rotate.previousActive` | 舊 Secret 仍有效至 {expiresAt}。 |
| `hub.site.revoke.action` | 立即撤銷 |
| `hub.site.revoke.explain` | 撤銷後此 Secret 立刻失效，使用它的網站會顯示「憑證失效」。若懷疑 Secret 外洩，請使用此功能。 |
| `hub.site.revoke.confirm` | 確定要立即撤銷這組 Secret 嗎？此動作無法復原。 |
| `hub.site.revoke.done` | Secret 已撤銷。 |

---

## 5. Donate 卡包兌換碼信與 App 兌換畫面

### 5.1 兌換碼信（Hub 寄出）

| Key | 文案 |
| --- | --- |
| `redeem.email.subject` | 你在 {streamerName} 獲得一包卡包，兌換碼在這裡 |
| `redeem.email.greeting` | 你好， |
| `redeem.email.body.intro` | 謝謝你支持 {streamerName}。你獲得了一包「{packName}」，請在 App 中開啟卡包查看抽到的卡牌。 |
| `redeem.email.body.codeLabel` | 你的兌換碼 |
| `redeem.email.body.code` | {redeemCode} |
| `redeem.email.body.expiry` | 兌換碼只能使用一次，請在 {expiresAt} 前兌換（{expiryDays} 天內有效）。 |
| `redeem.email.howto.title` | 如何兌換 |
| `redeem.email.howto.step1` | 1. 下載 EasyLottery Card App：App Store {appStoreUrl}／Google Play {googlePlayUrl} |
| `redeem.email.howto.step2` | 2. 用這個 email 登入（會收到 6 位數驗證碼）。 |
| `redeem.email.howto.step3` | 3. 點選「兌換卡包」，輸入上方兌換碼，即可開啟卡包。 |
| `redeem.email.body.safety` | 兌換碼等同卡包本身，請勿公開或提供給他人。如果你沒有參加 {streamerName} 的 Donate 活動，請忽略這封信。 |
| `redeem.email.body.noCashValue` | 卡牌僅供收藏與交換，無現金價值，不得轉售換取金錢。 [需法務確認] |
| `redeem.email.body.privacy` | 我們依你在 Donate 時的同意寄送此信。兌換或到期後 180 天，你的 email 會從此活動紀錄中刪除；如需提前刪除，請來信 {supportEmail}。 [需法務確認] |
| `redeem.email.signature` | EasyLottery Card Hub |
| `redeem.email.footer` | 這是系統自動寄出的信件，請勿直接回覆。 |

### 5.2 App 兌換畫面

| Key | 文案 |
| --- | --- |
| `app.redeem.title` | 兌換卡包 |
| `app.redeem.lead` | 輸入 Donate 後收到的兌換碼。 |
| `app.redeem.input.label` | 兌換碼 |
| `app.redeem.input.placeholder` | XXXX-XXXX-XXXX-XXXX-XXXX-XXXX |
| `app.redeem.input.helper` | 可直接貼上，大小寫與連字號不影響。 |
| `app.redeem.submit` | 兌換 |
| `app.redeem.submitting` | 兌換中… |
| `app.redeem.success` | 兌換成功，準備開啟卡包。 |
| `app.redeem.skipAnimation` | 略過動畫 |
| `app.redeem.error.used` | 這組兌換碼已經使用過。卡牌若在你的帳號中，可以到「我的收藏」查看。 |
| `app.redeem.error.expired` | 這組兌換碼已過期，無法兌換。如有疑問請聯絡實況主。 |
| `app.redeem.error.invalid` | 找不到這組兌換碼，請確認輸入是否正確。 |
| `app.redeem.error.void` | 這組兌換碼已失效，可能已由實況主重新寄送新的兌換碼，請查看最新的信件。 |
| `app.redeem.error.format` | 兌換碼格式不正確，應為 24 個字元。 |
| `app.redeem.error.rateLimited` | 嘗試次數過多，請在 {minutes} 分鐘後再試。 |
| `app.redeem.error.tenantDisabled` | {streamerName} 的卡牌目前暫停兌換，請稍後再試或聯絡實況主。 |
| `app.redeem.error.network` | 無法連線到 Card Hub，請檢查網路後重試。兌換碼不會因此失效。 |

---

## 6. 實況主停用／待刪除通知與「已下架」卡牌

| Key | 文案 |
| --- | --- |
| `app.tenant.disabled.banner` | {streamerName} 的卡牌目前暫停服務。你已擁有的卡牌仍可查看，但暫時無法兌換或交換。 |
| `app.tenant.pendingDeletion.banner` | {streamerName} 即將結束卡牌服務。{deletionDate} 後卡牌圖片將移除，卡名、序號與稀有度會保留在你的收藏中。 [需法務確認] |
| `app.tenant.pendingDeletion.detail` | 在此之前你仍可查看這些卡牌，也可以設為桌布保存。 |
| `app.tenant.pendingDeletion.email.subject` | {streamerName} 的卡牌將於 30 天後移除圖片 |
| `app.tenant.pendingDeletion.email.body` | {streamerName} 即將結束 Card Hub 卡牌服務。你收藏的卡牌圖片將在 {deletionDate} 移除；卡名、序號與稀有度會繼續保留在你的帳號中，不會被刪除或重新發出。 [需法務確認] |
| `app.card.removed.label` | 已下架 |
| `app.card.removed.desc` | 此卡牌的實況主已結束服務，圖片已移除。 |
| `app.card.removed.meta` | {cardName}・{serialNo}・{rarity} |
| `app.card.readOnly.tradeDisabled` | 此實況主的卡牌目前無法交換。 |

---

## 7. 機率揭露與「無現金價值」聲明

| Key | 文案 |
| --- | --- |
| `shop.odds.title` | 抽卡機率 |
| `shop.odds.line` | 每包抽出 1 張卡牌，各稀有度機率如下：{oddsList} |
| `shop.odds.item` | {rarity} {percent}% |
| `shop.odds.footnote` | 同一稀有度內的卡牌機率相同。數量有限的卡包售完後將停止抽卡。 |
| `shop.odds.preDonate` | Donate 前請先確認抽卡機率。 [需法務確認] |
| `shop.noCashValue` | 卡牌無現金價值、不得轉售換取金錢，僅供收藏與觀眾間交換。 [需法務確認] |
| `shop.emailConsent.checkbox` | 我同意提供 email，用於寄送卡包兌換碼。email 會在兌換或到期後 180 天刪除，如需提前刪除請聯絡 {supportEmail}。 [需法務確認] |
| `shop.emailConsent.missing` | 未填寫 email 或未勾選同意，此筆 Donate 仍會照常完成，但不會抽卡。 |
| `app.odds.line` | 抽卡機率：{oddsList} |
| `app.odds.link` | 查看抽卡機率 |
| `app.noCashValue` | 卡牌無現金價值、不得轉售換取金錢。 [需法務確認] |
| `app.trade.noCashValue` | 交換僅限卡牌互換，不提供任何金錢、點數或價格。 [需法務確認] |

---

## 需法務確認清單

1. `hub.invite.page.terms`、`viewer.signup.terms.checkbox`、`viewer.signup.terms.updated`：條款同意文字與版本更新流程。
2. `viewer.signup.age.minorNotice`：13–17 歲是否須法定代理人同意，以及如何呈現。
3. `viewer.signup.under13.body`：未滿 13 歲阻擋文字，以及「不保存 email」的承諾是否與實作一致。
4. `redeem.email.body.privacy`、`shop.emailConsent.checkbox`：個資蒐集目的、保存期間（180 天）與刪除管道；Card Hub 法律主體尚未決定。
5. `redeem.email.body.noCashValue`、`shop.noCashValue`、`app.noCashValue`、`app.trade.noCashValue`：無現金價值與禁止轉售聲明。
6. `shop.odds.preDonate`：機率揭露位置與用語是否符合 App Store／Google Play 及國內規範。
7. `app.tenant.pendingDeletion.*`：30 天通知內容與刪除後保留項目。

## 待確認事項（非法務）

- App 正式名稱（目前暫用「EasyLottery Card App」）、商店連結、客服信箱 `{supportEmail}`。
- 兌換碼效期預設 90 天仍為 Open decision，文案已用 `{expiryDays}` 參數化。
- 已決定（產品主管，2026-11-14）：保留 `app.redeem.error.void`。兌換碼為 120 bits 隨機值，無法列舉，揭露「曾存在」沒有安全風險，而提示捐贈者查看新信件可以減少客服負擔。
