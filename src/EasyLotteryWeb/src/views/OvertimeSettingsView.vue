<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from "vue";
import { ApiError } from "../api/client";
import { appendOvertimeEvent, type OvertimeSupportEvent } from "../overtime/api";
import {
  buildOvertimePreviewUrl,
  clearOvertimeFeed,
  createOvertimePreviewToken,
  extendPlannedEndForReward,
  loadOvertimeSettings,
  saveOvertimeSettings,
  saveOvertimeSnapshot,
  type OvertimeRewardRule,
  type OvertimeSettings
} from "../overtime/admin-api";

const settings = ref<OvertimeSettings | null>(null);
const plannedEndLocal = ref("");
const plannedEndInitialLocal = ref("");
const loading = ref(true);
const saving = ref(false);
const action = ref("");
const error = ref("");
const success = ref("");
const previewUrl = ref("");

const defaultRules: OvertimeRewardRule[] = [
  { isEnabled: true, amountThreshold: 100, addHours: 0, addMinutes: 10, label: "入門加班" },
  { isEnabled: true, amountThreshold: 300, addHours: 0, addMinutes: 30, label: "熱心支持" },
  { isEnabled: true, amountThreshold: 1000, addHours: 2, addMinutes: 0, label: "長時加班" }
];

function toLocalInput(value: string | null): string {
  if (!value) return "";
  const date = new Date(value);
  if (!Number.isFinite(date.getTime()) || date.getFullYear() < 2000) return "";
  return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
}

function fromLocalInput(value: string): string | null {
  if (!value) return null;
  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) throw new Error("請輸入有效的預計結束時間。");
  return date.toISOString();
}

async function reload(): Promise<void> {
  loading.value = true;
  error.value = "";
  success.value = "";
  try {
    const snapshot = await loadOvertimeSettings();
    settings.value = snapshot.data;
    settings.value.rewardRules ??= [];
    plannedEndLocal.value = toLocalInput(snapshot.data.plannedEndAtUtc);
    plannedEndInitialLocal.value = plannedEndLocal.value;
  } catch (cause) {
    settings.value = null;
    error.value = cause instanceof ApiError ? cause.message : "無法載入加班台設定。";
  } finally {
    loading.value = false;
  }
}

async function save(): Promise<void> {
  if (!settings.value || saving.value) return;
  saving.value = true;
  error.value = "";
  success.value = "";
  try {
    const preserveLatestPlannedEnd = plannedEndLocal.value === plannedEndInitialLocal.value;
    const draft = { ...settings.value, plannedEndAtUtc: fromLocalInput(plannedEndLocal.value) };
    const saved = await saveOvertimeSettings(draft, { preserveLatestPlannedEnd });
    settings.value = saved.data;
    plannedEndLocal.value = toLocalInput(saved.data.plannedEndAtUtc);
    plannedEndInitialLocal.value = plannedEndLocal.value;
    success.value = "已儲存加班台設定。OBS 正在使用的場次狀態已保留。";
  } catch (cause) {
    error.value = cause instanceof ApiError && cause.status === 409
      ? "設定已由 OBS 或其他管理員更新。請重新載入最新設定後再儲存。"
      : cause instanceof ApiError ? cause.message : cause instanceof Error ? cause.message : "加班台設定儲存失敗。";
  } finally {
    saving.value = false;
  }
}

function addRule(): void {
  settings.value?.rewardRules.push({ isEnabled: true, amountThreshold: 100, addHours: 0, addMinutes: 30, label: "新規則" });
}

function resetRules(): void {
  if (settings.value) settings.value.rewardRules = defaultRules.map(rule => ({ ...rule }));
}

function randomId(): string {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function amountText(amount: number): string {
  return `NT$${amount.toLocaleString("zh-TW", { maximumFractionDigits: 2 })}`;
}

async function sendDemo(kind: "superchat" | "ecpay"): Promise<void> {
  if (!settings.value || action.value) return;
  action.value = kind;
  error.value = "";
  success.value = "";
  let eventAdded = false;
  try {
    const snapshot = await loadOvertimeSettings();
    const now = new Date();
    const amount = Math.max(0, kind === "superchat" ? settings.value.demoSuperChatAmount : settings.value.demoEcpayAmount);
    const endAt = extendPlannedEndForReward(snapshot.data, amount, now);
    const isRunning = snapshot.data.sessionState.toLowerCase() === "running";
    const event: OvertimeSupportEvent = {
      source: kind === "superchat" ? 1 : 2,
      sourceLabel: kind === "superchat" ? "YouTube SuperChat" : "ECPay Donate",
      displayName: kind === "superchat" ? "示範觀眾" : "示範贊助者",
      message: kind === "superchat" ? "祝直播順利，這是示範 SuperChat。" : "綠界示範贊助。",
      amount,
      amountDisplay: amountText(amount),
      currency: "TWD",
      avatarUrl: "",
      color: kind === "superchat" ? "#ff7ab6" : "#ffd166",
      externalId: randomId(),
      occurredAtUtc: now.toISOString(),
      sessionEndAtUtc: endAt
    };
    await appendOvertimeEvent(event);
    eventAdded = true;

    if (isRunning && endAt !== snapshot.data.plannedEndAtUtc) {
      const saved = await saveOvertimeSnapshot(snapshot, { ...snapshot.data, plannedEndAtUtc: endAt });
      settings.value.plannedEndAtUtc = saved.data.plannedEndAtUtc;
      plannedEndLocal.value = toLocalInput(saved.data.plannedEndAtUtc);
      plannedEndInitialLocal.value = plannedEndLocal.value;
      await appendOvertimeEvent({
        source: 3,
        sourceLabel: "預計關台時間",
        displayName: "預計關台時間",
        message: "預計關台時間已更新。",
        amount: null,
        amountDisplay: "",
        currency: "TWD",
        avatarUrl: "",
        color: "#8bd3ff",
        externalId: randomId(),
        occurredAtUtc: new Date().toISOString(),
        sessionEndAtUtc: saved.data.plannedEndAtUtc
      });
    }
    success.value = kind === "superchat" ? "已送出示範 SuperChat。" : "已送出示範綠界 Donate。";
  } catch (cause) {
    error.value = eventAdded
      ? "示範訊息已加入，但加班時間同步失敗；請重新載入並檢查場次狀態。"
      : cause instanceof ApiError ? cause.message : "示範訊息送出失敗。";
  } finally {
    action.value = "";
  }
}

async function clearFeed(): Promise<void> {
  if (action.value || !window.confirm("確定清除加班台訊息嗎？此操作會移除目前顯示的訊息。")) return;
  action.value = "clear";
  error.value = "";
  success.value = "";
  try {
    await clearOvertimeFeed();
    success.value = "已清除加班台訊息。";
  } catch (cause) {
    error.value = cause instanceof ApiError ? cause.message : "清除加班台訊息失敗。";
  } finally {
    action.value = "";
  }
}

async function createPreview(): Promise<void> {
  action.value = "preview";
  error.value = "";
  success.value = "";
  try {
    const token = await createOvertimePreviewToken();
    previewUrl.value = buildOvertimePreviewUrl(window.location.origin, token.data.token);
    success.value = "已建立加班台 OBS 預覽連結。";
  } catch (cause) {
    error.value = cause instanceof ApiError ? cause.message : "無法建立 OBS 預覽連結。";
  } finally {
    action.value = "";
  }
}

async function copyPreview(): Promise<void> {
  if (!previewUrl.value) return;
  try {
    await navigator.clipboard.writeText(previewUrl.value);
    success.value = "已複製 OBS 預覽連結。";
  } catch {
    error.value = "瀏覽器未允許複製；請使用「開啟預覽」或檢查剪貼簿權限。";
  }
}

onMounted(() => void reload());
onBeforeUnmount(() => { previewUrl.value = ""; });
</script>

<template>
  <main class="settings-page overtime-settings-page" aria-labelledby="overtime-title">
    <header class="page-heading">
      <p class="eyebrow">LIVE SUPPORT OVERLAY</p>
      <h1 id="overtime-title">加班台設定</h1>
      <p class="lead">調整畫面、獎勵規則與 OBS 預覽。開始、暫停與結束場次仍由 OBS 控制。</p>
    </header>

    <v-alert v-if="error" type="error" variant="tonal" role="alert" class="mb-4">
      {{ error }}
      <template #append><v-btn variant="text" @click="reload">重新載入</v-btn></template>
    </v-alert>
    <p v-if="success" class="success-text mb-4" role="status">{{ success }}</p>
    <div v-if="loading" class="state-card" role="status">加班台設定載入中…</div>

    <template v-else-if="settings">
      <v-row>
        <v-col cols="12" xl="7">
          <v-card class="settings-card" rounded="xl" elevation="1">
            <v-card-title>畫面與顯示</v-card-title>
            <v-card-text>
              <v-text-field v-model="settings.title" label="標題" placeholder="例如：加班台" variant="outlined" />
              <v-text-field v-model="settings.subtitle" label="副標題" placeholder="例如：SuperChat / 綠界贊助" variant="outlined" />
              <v-row>
                <v-col cols="12" sm="6">
                  <v-text-field v-model.number="settings.maxVisibleItems" label="最大顯示筆數" type="number" min="1" max="30" variant="outlined" />
                </v-col>
                <v-col cols="12" sm="6">
                  <v-text-field v-model.number="settings.supportMessageVisibleSeconds" label="單行訊息顯示秒數" type="number" min="1" max="60" variant="outlined" />
                </v-col>
              </v-row>
              <div class="switch-row">
                <v-switch v-model="settings.showAmounts" label="顯示金額" color="primary" hide-details />
                <v-switch v-model="settings.showAvatars" label="顯示頭像" color="primary" hide-details />
              </div>

              <v-divider class="my-5" />
              <h2 class="section-title">預計結束時間</h2>
              <p class="field-hint">場次開始後，符合獎勵規則的贊助會延長結束時間。</p>
              <v-text-field v-model="plannedEndLocal" label="預計結束時間" type="datetime-local" step="1" variant="outlined" />

              <v-divider class="my-5" />
              <h2 class="section-title">訊息動畫與達標煙火</h2>
              <v-select
                v-model="settings.textAnimationKey"
                label="單行文字動畫"
                :items="[{ title: '滑入', value: 'slide-in' }, { title: '淡入', value: 'fade-in' }, { title: '彈出', value: 'pop-in' }, { title: '無動畫', value: 'none' }]"
                variant="outlined"
              />
              <v-switch v-model="settings.enableCompletionFireworks" label="啟用達標煙火" color="primary" hide-details />
              <v-row>
                <v-col cols="12" sm="4"><v-select v-model="settings.completionFireworksStyle" label="煙火風格" :items="[{ title: '經典煙火', value: 'classic' }, { title: '像素煙火', value: 'pixel' }]" :disabled="!settings.enableCompletionFireworks" variant="outlined" /></v-col>
                <v-col cols="12" sm="4"><v-text-field v-model.number="settings.completionFireworksDurationSeconds" label="播放秒數" type="number" min="2" max="15" :disabled="!settings.enableCompletionFireworks" variant="outlined" /></v-col>
                <v-col cols="12" sm="4"><v-text-field v-model="settings.completionFireworksGifUrl" label="自訂煙火 GIF 網址" placeholder="選填" :disabled="!settings.enableCompletionFireworks" variant="outlined" /></v-col>
              </v-row>
            </v-card-text>
          </v-card>

          <v-card class="settings-card mt-5" rounded="xl" elevation="1">
            <v-card-title>金額換算加班時間</v-card-title>
            <v-card-subtitle>每筆贊助達到門檻時，延長設定的時數或分鐘。</v-card-subtitle>
            <v-card-text>
              <div v-if="settings.rewardRules.length === 0" class="state-card mb-3">目前沒有獎勵規則。</div>
              <v-card v-for="(rule, index) in settings.rewardRules" :key="index" class="reward-rule mb-3" variant="tonal">
                <v-card-text>
                  <div class="rule-header">
                    <v-text-field v-model="rule.label" label="規則名稱" variant="outlined" density="comfortable" />
                    <v-btn icon="mdi-delete-outline" variant="text" color="error" :aria-label="`刪除規則 ${rule.label || index + 1}`" @click="settings?.rewardRules.splice(index, 1)" />
                  </div>
                  <v-row>
                    <v-col cols="12" sm="3"><v-text-field v-model.number="rule.amountThreshold" label="門檻金額" type="number" min="0" variant="outlined" density="comfortable" /></v-col>
                    <v-col cols="12" sm="3"><v-text-field v-model.number="rule.addHours" label="加幾小時" type="number" min="0" variant="outlined" density="comfortable" /></v-col>
                    <v-col cols="12" sm="3"><v-text-field v-model.number="rule.addMinutes" label="加幾分鐘" type="number" min="0" variant="outlined" density="comfortable" /></v-col>
                    <v-col cols="12" sm="3"><v-switch v-model="rule.isEnabled" label="啟用" color="primary" hide-details /></v-col>
                  </v-row>
                </v-card-text>
              </v-card>
              <div class="rule-actions">
                <v-btn variant="tonal" @click="resetRules">重設預設</v-btn>
                <v-btn color="primary" variant="tonal" @click="addRule">新增規則</v-btn>
              </div>
            </v-card-text>
          </v-card>

          <v-card class="settings-card mt-5" rounded="xl" elevation="1">
            <v-card-title>示範通知</v-card-title>
            <v-card-text>
              <v-row>
                <v-col cols="12" sm="6"><v-text-field v-model.number="settings.demoSuperChatAmount" label="示範 SuperChat 金額" type="number" min="0" variant="outlined" /></v-col>
                <v-col cols="12" sm="6"><v-text-field v-model.number="settings.demoEcpayAmount" label="示範綠界金額" type="number" min="0" variant="outlined" /></v-col>
              </v-row>
              <p class="field-hint">示範只會寫入加班台測試訊息，不會連線或扣款給任何平台。</p>
              <div class="rule-actions">
                <v-btn color="success" :loading="action === 'superchat'" :disabled="!!action" @click="sendDemo('superchat')">送出示範 SuperChat</v-btn>
                <v-btn color="warning" :loading="action === 'ecpay'" :disabled="!!action" @click="sendDemo('ecpay')">送出示範綠界 Donate</v-btn>
                <v-btn color="error" variant="tonal" :loading="action === 'clear'" :disabled="!!action" @click="clearFeed">清除加班台訊息</v-btn>
              </div>
            </v-card-text>
          </v-card>

          <div class="save-actions">
            <v-btn color="primary" size="large" :loading="saving" :disabled="saving || !!action" @click="save">儲存設定</v-btn>
          </div>
        </v-col>

        <v-col cols="12" xl="5">
          <v-card class="preview-card" rounded="xl" elevation="4" aria-label="加班台動畫預覽">
            <v-card-text>
              <p class="preview-eyebrow">LIVE SUPPORT · PREVIEW</p>
              <h2 class="preview-title">{{ settings.title || "加班台" }}</h2>
              <p class="preview-subtitle">{{ settings.subtitle || "SuperChat / 綠界贊助" }}</p>
              <div class="preview-event" :class="`animation-${settings.textAnimationKey}`">
                <div class="preview-avatar" aria-hidden="true">示</div>
                <div class="preview-copy"><strong>示範觀眾</strong><span>YouTube SuperChat</span><p>祝直播順利，這是示範訊息。</p></div>
                <strong v-if="settings.showAmounts" class="preview-amount">{{ amountText(settings.demoSuperChatAmount) }}</strong>
              </div>
              <div class="firework-glow" :class="{ disabled: !settings.enableCompletionFireworks }" aria-hidden="true"><span>✦</span><span>✧</span><span>✦</span></div>
              <p class="preview-footnote">{{ settings.maxVisibleItems }} 則訊息 · {{ settings.completionFireworksStyle === "pixel" ? "像素" : "經典" }}煙火{{ settings.enableCompletionFireworks ? "已啟用" : "已停用" }}</p>
            </v-card-text>
          </v-card>

          <v-card class="settings-card mt-5" rounded="xl" elevation="1">
            <v-card-title>OBS 預覽連結</v-card-title>
            <v-card-text>
              <p class="field-hint">連結使用限定於 overtime/default 的 OBS 權杖；權杖只放在網址片段中，不會顯示在文字或送往其他 API。</p>
              <v-btn color="primary" variant="tonal" :loading="action === 'preview'" :disabled="!!action" @click="createPreview">建立預覽連結</v-btn>
              <div v-if="previewUrl" class="preview-link-actions mt-3">
                <v-btn :href="previewUrl" target="_blank" rel="noopener noreferrer" variant="text">開啟預覽</v-btn>
                <v-btn variant="text" @click="copyPreview">複製連結</v-btn>
              </div>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </template>
  </main>
</template>

<style scoped>
.overtime-settings-page { max-width: 1440px; margin-inline: auto; }
.settings-card { border: 1px solid rgba(37, 99, 235, 0.1); }
.section-title { margin-bottom: 0.75rem; font-size: 1.05rem; font-weight: 750; }
.switch-row, .rule-actions, .save-actions, .preview-link-actions { display: flex; align-items: center; flex-wrap: wrap; gap: 0.65rem; }
.save-actions { justify-content: flex-end; padding-block: 1.5rem; }
.rule-header { display: flex; align-items: flex-start; gap: 0.5rem; }
.rule-header .v-input { flex: 1; }
.field-hint { color: #64748b; font-size: 0.9rem; }
.state-card { padding: 1rem; color: #526179; background: #f5f8fc; border: 1px solid #dbe4f0; border-radius: 1rem; }
.success-text { color: #147a43; font-weight: 650; }
.preview-card { position: sticky; top: 5.5rem; overflow: hidden; color: white; background: radial-gradient(ellipse at 12% 4%, rgba(255, 186, 100, .28), transparent 38%), radial-gradient(ellipse at 90% 0%, rgba(236, 72, 153, .27), transparent 40%), linear-gradient(145deg, #17153d, #302455 58%, #682a68); }
.preview-card::before, .preview-card::after { position: absolute; pointer-events: none; content: ""; width: 10rem; height: 10rem; border-radius: 50%; filter: blur(48px); opacity: .35; animation: preview-drift 7s ease-in-out infinite alternate; }
.preview-card::before { top: 18%; right: -4rem; background: #fb7185; }
.preview-card::after { bottom: -4rem; left: -3rem; background: #fbbf24; animation-delay: -3s; }
.preview-card :deep(.v-card-text) { position: relative; z-index: 1; padding: clamp(1.25rem, 3vw, 2rem); }
.preview-eyebrow { color: #fcd34d; font-size: .72rem; font-weight: 800; letter-spacing: .18em; }
.preview-title { margin-top: .6rem; font-size: clamp(1.8rem, 4vw, 2.5rem); font-weight: 900; line-height: 1.15; text-shadow: 0 3px 24px rgba(251, 191, 36, .28); }
.preview-subtitle { margin-top: .45rem; color: rgba(255,255,255,.77); }
.preview-event { display: flex; align-items: center; gap: .9rem; margin-top: 2rem; padding: 1rem; border: 1px solid rgba(255,255,255,.16); border-radius: 1.15rem; background: rgba(12, 12, 36, .48); box-shadow: 0 18px 45px rgba(0,0,0,.2); backdrop-filter: blur(12px); }
.preview-avatar { flex: none; display: grid; place-items: center; width: 2.8rem; height: 2.8rem; border: 1px solid rgba(255,255,255,.4); border-radius: 1rem; background: linear-gradient(145deg, #fb7185, #fbbf24); font-weight: 900; box-shadow: 0 0 22px rgba(251, 191, 36, .23); }
.preview-copy { min-width: 0; flex: 1; }
.preview-copy strong, .preview-copy span { display: block; }
.preview-copy span { color: rgba(255,255,255,.68); font-size: .8rem; }
.preview-copy p { margin: .45rem 0 0; color: rgba(255,255,255,.9); font-size: .9rem; }
.preview-amount { color: #fde68a; white-space: nowrap; }
.firework-glow { display: flex; justify-content: space-around; margin-top: 1.8rem; color: #fcd34d; text-shadow: 0 0 15px #f59e0b; }
.firework-glow span:nth-child(1) { animation: twinkle 1.8s ease-in-out infinite; font-size: 1.35rem; }
.firework-glow span:nth-child(2) { animation: twinkle 2.4s ease-in-out infinite reverse; font-size: 2rem; color: #f9a8d4; }
.firework-glow span:nth-child(3) { animation: twinkle 1.5s ease-in-out infinite; font-size: 1.1rem; color: #a5f3fc; }
.firework-glow.disabled { opacity: .3; }
.preview-footnote { margin: 1rem 0 0; color: rgba(255,255,255,.68); font-size: .82rem; }
.animation-slide-in { animation: sample-slide .65s cubic-bezier(.2,.8,.2,1) both; }
.animation-fade-in { animation: sample-fade .65s ease both; }
.animation-pop-in { animation: sample-pop .55s cubic-bezier(.2,1.6,.5,1) both; }
.animation-none { animation: none; }
@keyframes preview-drift { to { transform: translate(-1rem, .8rem) scale(1.12); opacity: .48; } }
@keyframes twinkle { 50% { transform: translateY(-5px) scale(1.15); opacity: .72; } }
@keyframes sample-slide { from { opacity: 0; transform: translateX(-1rem); } to { opacity: 1; transform: translateX(0); } }
@keyframes sample-fade { from { opacity: .15; } to { opacity: 1; } }
@keyframes sample-pop { from { opacity: .3; transform: scale(.94); } to { opacity: 1; transform: scale(1); } }
@media (prefers-reduced-motion: reduce) { .preview-card::before, .preview-card::after, .firework-glow span, .preview-event { animation: none !important; transition: none !important; } }
@media (max-width: 1279px) { .preview-card { position: relative; top: auto; } }
</style>
