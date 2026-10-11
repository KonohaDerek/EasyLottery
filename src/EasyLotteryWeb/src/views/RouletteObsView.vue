<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { HubConnectionBuilder } from "@microsoft/signalr";
import { useRoute } from "vue-router";
import { ApiError, apiRequest } from "../api/client";
import { readObsSessionToken } from "../obs/session-token";
import { reconnectLiveDraw } from "../obs/reconnect";
import type { RouletteTemplate } from "../roulette/template";

type Phase = 0 | 1 | 2 | 3;
interface LiveDrawState {
  publicId: string;
  revision: number;
  phase: Phase;
  phaseEndsAtUtc: string | null;
  resultTitle: string | null;
  rouletteSegmentIndex: number | null;
  rouletteRotationDeg: number;
}
interface SpinResult {
  segmentIndex: number;
  segmentTitle: string;
  totalRotationDeg: number;
}
interface ActivityRecord {
  activityName: string;
  activityDateUtc: string;
  summary: string;
  templatePublicId: string;
  items: { order: number; name: string; description: string }[];
}

const route = useRoute();
const publicId = String(route.params.publicId ?? "");
const controlsEnabled = computed(() => route.query.controls === "1");
const motionReduced = computed(() => route.query.motion === "reduce");
const lowQuality = computed(() => route.query.quality === "low");
const loading = ref(true);
const template = ref<RouletteTemplate | null>(null);
const error = ref("");
const statusMessage = ref("");
const connectionStatus = ref("連線中…");
const canRetryConnection = ref(false);
const retryingConnection = ref(false);
const flowPhase = ref<Phase>(0);
const countdownRemaining = ref<number | null>(null);
const currentRotation = ref(0);
const lastResult = ref<SpinResult | null>(null);
const isRecording = ref(false);
const lastRecordedSignature = ref("");
const activePreset = ref("arcade-stage");
const accentGradient = ref("linear-gradient(135deg, #ffcb47, #ff9f1a)");
let appliedRevision = -1;
let connection: Awaited<ReturnType<HubConnectionBuilder["build"]>> | null = null;
let countdownTimer: number | undefined;
let audioContext: AudioContext | null = null;

const isBusy = computed(() => flowPhase.value === 1 || flowPhase.value === 2);
const isDrawInProgress = computed(() => isBusy.value || flowPhase.value === 3);
const isIdle = computed(() => flowPhase.value === 0 && lastResult.value === null);
const isMissResult = computed(() => /銘謝惠顧|未中獎/iu.test(lastResult.value?.segmentTitle ?? ""));
const canRecord = computed(() => !!template.value && !!lastResult.value && !isBusy.value && buildRecordSignature() !== lastRecordedSignature.value);
const rotationStyle = computed(() => ({
  transform: `rotate(${currentRotation.value}deg)`,
  transitionDuration: flowPhase.value === 2 ? `${template.value?.spinDurationSec ?? 5}s` : "0s"
}));
const wrapperStyle = computed(() => ({
  "--obs-accent-gradient": accentGradient.value,
  backgroundImage: template.value?.backgroundImageUrl ? `url(${JSON.stringify(template.value.backgroundImageUrl)})` : undefined
}));

const layoutGradients: Record<string, string> = {
  "stage-spotlight": "linear-gradient(135deg, #ffcb47, #ff9f1a)",
  "neon-panel": "linear-gradient(135deg, #63d4ff, #8b5cf6)",
  "festival-pane": "linear-gradient(135deg, #ffd36c, #ef6b6b)"
};
const cues: Record<string, { countdown: string; spin: string; result: string }> = {
  "arcade-stage": { countdown: "tone:784:140:square", spin: "tone:988:180:triangle", result: "tone:1046:240:triangle" },
  "soft-stage": { countdown: "tone:523:160:sine", spin: "tone:659:220:triangle", result: "tone:740:260:triangle" },
  "retro-pixel": { countdown: "tone:880:100:square", spin: "tone:1175:160:square", result: "tone:1319:220:triangle" }
};

function describeSector(startAngle: number, endAngle: number): string {
  const radius = 180;
  const start = startAngle * Math.PI / 180;
  const end = endAngle * Math.PI / 180;
  const x1 = 200 + radius * Math.cos(start);
  const y1 = 200 + radius * Math.sin(start);
  const x2 = 200 + radius * Math.cos(end);
  const y2 = 200 + radius * Math.sin(end);
  return `M 200.00 200.00 L ${x1.toFixed(2)} ${y1.toFixed(2)} A 180.00 180.00 0 ${endAngle - startAngle > 180 ? 1 : 0} 1 ${x2.toFixed(2)} ${y2.toFixed(2)} Z`;
}

function friendlyError(cause: unknown): string {
  if (cause instanceof ApiError && [401, 403].includes(cause.status)) {
    return "OBS 工作階段無效或已過期，請回管理頁重新開啟預覽。";
  }
  if (cause instanceof ApiError && cause.status === 429) return "請求過於頻繁，稍候再試。";
  return cause instanceof Error ? cause.message : "無法載入轉盤 OBS 畫面。";
}

async function loadSettings(): Promise<void> {
  const [layout, sound] = await Promise.allSettled([
    apiRequest<{ activeLayoutKey: string }>("/api/settings/obs-layout"),
    apiRequest<{ activePresetKey: string }>("/api/settings/sound-cues")
  ]);
  if (layout.status === "fulfilled") accentGradient.value = layoutGradients[layout.value.data.activeLayoutKey] ?? layoutGradients["stage-spotlight"];
  if (sound.status === "fulfilled" && cues[sound.value.data.activePresetKey]) activePreset.value = sound.value.data.activePresetKey;
}

async function playCue(cue: string | undefined): Promise<void> {
  if (!cue) return;
  const [kind, frequency, duration, waveType] = cue.split(":");
  if (kind !== "tone") return;
  try {
    audioContext ??= new AudioContext();
    if (audioContext.state === "suspended") await audioContext.resume();
    const oscillator = audioContext.createOscillator();
    const gain = audioContext.createGain();
    const seconds = Math.max(0.01, Number(duration) / 1000 || 0.18);
    oscillator.type = waveType as OscillatorType;
    oscillator.frequency.value = Number(frequency) || 440;
    gain.gain.setValueAtTime(0.0001, audioContext.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.12, audioContext.currentTime + 0.01);
    gain.gain.exponentialRampToValueAtTime(0.0001, audioContext.currentTime + seconds);
    oscillator.connect(gain);
    gain.connect(audioContext.destination);
    oscillator.start();
    oscillator.stop(audioContext.currentTime + seconds);
  } catch {
    // Browser autoplay restrictions must not block live draw updates.
  }
}

function updateCountdown(endsAt: string | null): void {
  window.clearInterval(countdownTimer);
  countdownTimer = undefined;
  if (flowPhase.value !== 1 || !endsAt) {
    countdownRemaining.value = null;
    return;
  }
  const tick = () => {
    countdownRemaining.value = Math.max(1, Math.ceil((Date.parse(endsAt) - Date.now()) / 1000));
  };
  tick();
  countdownTimer = window.setInterval(tick, 200);
}

function applyLiveState(state: LiveDrawState): void {
  if (state.publicId.toLowerCase() !== publicId.toLowerCase() || state.revision < appliedRevision) return;
  appliedRevision = state.revision;
  flowPhase.value = state.phase;
  updateCountdown(state.phaseEndsAtUtc);
  if (state.phase === 2 || state.phase === 3) currentRotation.value = state.rouletteRotationDeg;
  if (state.phase === 3 && state.rouletteSegmentIndex !== null) {
    lastResult.value = {
      segmentIndex: state.rouletteSegmentIndex,
      segmentTitle: state.resultTitle ?? "",
      totalRotationDeg: state.rouletteRotationDeg
    };
  } else {
    lastResult.value = null;
  }
  const preset = cues[activePreset.value] ?? cues["arcade-stage"];
  if (state.phase === 1) void playCue(preset.countdown);
  if (state.phase === 2) void playCue(preset.spin);
  if (state.phase === 3) void playCue(preset.result);
}

async function readSessionToken(): Promise<string> {
  const token = readObsSessionToken();
  if (!token) throw new Error("OBS 工作階段 token 不存在，請從管理頁重新開啟預覽。");
  return token;
}

async function refreshLiveState(): Promise<void> {
  try {
    const response = await apiRequest<LiveDrawState>(`/api/live-draw/roulette/${publicId}`);
    applyLiveState(response.data);
  } catch (cause) {
    if (cause instanceof ApiError && cause.status === 404) {
      applyLiveState({ publicId, revision: appliedRevision + 1, phase: 0, phaseEndsAtUtc: null, resultTitle: null, rouletteSegmentIndex: null, rouletteRotationDeg: currentRotation.value });
      return;
    }
    throw cause;
  }
}

async function joinAndRefresh(): Promise<void> {
  if (!connection) return;
  await connection.invoke("JoinSession", "roulette", publicId, await readSessionToken());
  await refreshLiveState();
}

async function retryConnection(): Promise<void> {
  if (!connection || retryingConnection.value) return;
  retryingConnection.value = true;
  canRetryConnection.value = false;
  connectionStatus.value = "正在重新連線…";
  try {
    await reconnectLiveDraw(connection, () => { appliedRevision = -1; }, joinAndRefresh);
    connectionStatus.value = "即時連線已恢復";
    error.value = "";
    statusMessage.value = "";
  } catch (cause) {
    canRetryConnection.value = true;
    connectionStatus.value = "重新連線失敗，請再試一次";
    statusMessage.value = friendlyError(cause);
  } finally {
    retryingConnection.value = false;
  }
}

async function connect(): Promise<void> {
  connectionStatus.value = "連線中…";
  const next = new HubConnectionBuilder().withUrl("/hubs/live-draw").withAutomaticReconnect().build();
  connection = next;
  next.on("LiveDrawStateChanged", (state: LiveDrawState) => { void applyLiveState(state); });
  next.onreconnecting(() => {
    canRetryConnection.value = false;
    connectionStatus.value = "即時連線中斷，正在重新連線…";
  });
  next.onreconnected(async () => {
    try {
      // A server restart can reset its in-memory revision counter; the new snapshot is authoritative.
      appliedRevision = -1;
      await joinAndRefresh();
      connectionStatus.value = "即時連線已恢復";
      canRetryConnection.value = false;
      error.value = "";
    } catch (cause) {
      connectionStatus.value = "即時連線已恢復，但重新加入失敗";
      statusMessage.value = friendlyError(cause);
      canRetryConnection.value = true;
    }
  });
  next.onclose(() => {
    connectionStatus.value = "即時連線已中斷";
    canRetryConnection.value = true;
  });
  await next.start();
  await joinAndRefresh();
  connectionStatus.value = "即時連線正常";
  canRetryConnection.value = false;
}

async function initialize(): Promise<void> {
  loading.value = true;
  error.value = "";
  try {
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(publicId)) {
      throw new Error("轉盤 OBS 網址無效。");
    }
    template.value = (await apiRequest<RouletteTemplate>(`/api/roulette-templates/public/${publicId}`)).data;
    await loadSettings();
    await connect();
  } catch (cause) {
    error.value = friendlyError(cause);
    connectionStatus.value = "無法建立即時連線";
  } finally {
    loading.value = false;
  }
}

async function startSpin(useCountdown: boolean): Promise<void> {
  if (!template.value || isDrawInProgress.value) return;
  statusMessage.value = "";
  try {
    await apiRequest(`/api/live-draw/roulette/${publicId}/spin`, {
      method: "POST",
      body: JSON.stringify({ forceIndex: null, countdownSeconds: useCountdown ? 3 : 0 })
    });
  } catch (cause) {
    statusMessage.value = friendlyError(cause);
  }
}

function buildRecordSignature(): string {
  if (!template.value || !lastResult.value) return "";
  return `${template.value.id}:${lastResult.value.segmentIndex}:${lastResult.value.segmentTitle}:${currentRotation.value.toFixed(4)}`;
}

async function recordResult(): Promise<void> {
  if (!template.value || !lastResult.value || !canRecord.value || isRecording.value) return;
  isRecording.value = true;
  try {
    const record = (await apiRequest<ActivityRecord>(`/api/roulette-templates/public/${publicId}/record-result`, {
      method: "POST",
      body: JSON.stringify(lastResult.value)
    })).data;
    lastRecordedSignature.value = buildRecordSignature();
    const details = record.items.length
      ? `\n\n結果明細：\n${[...record.items].sort((a, b) => a.order - b.order).map(item => `${item.order}. ${item.name} - ${item.description}`).join("\n")}`
      : "";
    void apiRequest("/api/result-notification", {
      method: "POST",
      body: JSON.stringify({
        subject: `EasyLottery 結果通知 - ${record.activityName}`,
        body: `EasyLottery 結果通知\n活動名稱：${record.activityName}\n活動時間：${new Intl.DateTimeFormat("zh-TW", { year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", second: "2-digit", hourCycle: "h23" }).format(new Date(record.activityDateUtc))}\n摘要：${record.summary}${details}`,
        resourceKind: "roulette",
        resourceId: record.templatePublicId
      })
    }).catch(() => undefined);
    statusMessage.value = `已記錄活動「${record.activityName}」結果。`;
  } catch (cause) {
    statusMessage.value = friendlyError(cause);
  } finally {
    isRecording.value = false;
  }
}

function reload(): void {
  window.location.reload();
}

onMounted(() => { void initialize(); });
onBeforeUnmount(() => {
  window.clearInterval(countdownTimer);
  if (connection) void connection.stop();
  if (audioContext) void audioContext.close();
});
</script>

<template>
  <main class="obs-layout" :class="{ 'obs-controls-enabled': controlsEnabled, 'obs-motion-reduced': motionReduced, 'obs-quality-low': lowQuality }">
    <div v-if="loading" class="sr-only" role="status">正在載入轉盤 OBS 畫面…</div>
    <div v-else-if="error && controlsEnabled" class="obs-state obs-state-error" role="alert">
      <strong>無法載入轉盤 OBS</strong>
      <p>{{ error }}</p>
      <button v-if="canRetryConnection && template" type="button" :disabled="retryingConnection" @click="retryConnection">重新連線</button>
      <button v-else type="button" @click="reload">重新載入</button>
    </div>
    <div v-else-if="error" class="sr-only" role="status">{{ error }}</div>
    <template v-else-if="template">
      <div class="obs-roulette-wrapper" :class="isIdle ? 'obs-animation-idle' : 'obs-animation-visible'" :style="wrapperStyle">
        <section class="obs-stage">
        <div class="roulette-container" role="button" tabindex="0" aria-label="點擊轉盤開始抽獎" title="點擊轉盤開始抽獎" @click="startSpin(true)" @keydown.enter.prevent="startSpin(true)" @keydown.space.prevent="startSpin(true)">
          <div v-if="flowPhase === 2" class="roulette-spinning-aura"></div>
          <div class="roulette-pointer">
            <img v-if="template.pointerImageUrl" :src="template.pointerImageUrl" alt="指針" />
            <div v-else class="default-pointer">▼</div>
          </div>
          <div class="roulette-wheel-wrapper">
            <svg class="roulette-wheel" :class="{ spinning: flowPhase === 2 }" viewBox="0 0 400 400" :style="rotationStyle" aria-label="轉盤">
              <g v-for="(segment, index) in [...template.segments].sort((a, b) => a.index - b.index)" :key="segment.id || index">
                <path :d="describeSector(index * 360 / template.segments.length - 90, (index + 1) * 360 / template.segments.length - 90)" :fill="segment.color" stroke="rgba(255,255,255,.5)" stroke-width="2" :class="{ 'winner-segment': lastResult?.segmentIndex === index && flowPhase === 3 }" />
                <text :x="200 + 120 * Math.cos((index * 360 / template.segments.length - 90 + 180 / template.segments.length) * Math.PI / 180)" :y="200 + 120 * Math.sin((index * 360 / template.segments.length - 90 + 180 / template.segments.length) * Math.PI / 180)" text-anchor="middle" dominant-baseline="middle" font-size="14" fill="#fff" font-weight="bold" :transform="`rotate(${(index + .5) * 360 / template.segments.length - 90}, ${200 + 120 * Math.cos((index * 360 / template.segments.length - 90 + 180 / template.segments.length) * Math.PI / 180)}, ${200 + 120 * Math.sin((index * 360 / template.segments.length - 90 + 180 / template.segments.length) * Math.PI / 180)})`">{{ segment.title }}</text>
              </g>
              <image v-if="template.centerImageUrl" :href="template.centerImageUrl" x="160" y="160" width="80" height="80" />
              <circle v-else cx="200" cy="200" r="30" fill="#fff" />
            </svg>
          </div>
          <div v-if="lastResult && flowPhase === 3" class="obs-win-result" :class="{ 'miss-result': isMissResult }" role="status" aria-live="polite" aria-atomic="true">
            <span class="win-label">{{ isMissResult ? "結果：" : "🎉 中獎：" }}</span>
            <span class="win-title">{{ lastResult.segmentTitle }}</span>
          </div>
        </div>

        <div v-if="controlsEnabled" class="obs-controls">
          <button class="obs-spin-btn" type="button" :disabled="isDrawInProgress" @click="startSpin(true)">{{ flowPhase === 1 ? `倒數中… ${countdownRemaining ?? ""}` : "⏳ 倒數 3 秒後轉動" }}</button>
          <button class="obs-spin-btn" type="button" :disabled="isDrawInProgress" @click="startSpin(false)">{{ flowPhase === 2 ? "轉動中…" : "🎰 立即轉動" }}</button>
          <button class="obs-record-btn" type="button" :disabled="!canRecord || isRecording" @click="recordResult">{{ isRecording ? "記錄中…" : "結束本次活動並記錄" }}</button>
          <p role="status" aria-live="polite">{{ connectionStatus }}</p>
          <p v-if="statusMessage" role="alert">{{ statusMessage }}</p>
        </div>
        </section>
      </div>
      <div v-if="canRetryConnection" class="obs-reconnect-notice" role="alert" aria-live="assertive">
        <span>{{ connectionStatus }}</span>
        <button type="button" :disabled="retryingConnection" @click="retryConnection">{{ retryingConnection ? "連線中…" : "重新連線" }}</button>
      </div>
    </template>
  </main>
</template>

<style scoped>
.obs-layout { width: 100vw; height: 100vh; display: flex; align-items: center; justify-content: center; overflow: hidden; color: #fff; }
.obs-motion-reduced *, .obs-quality-low .obs-stage { animation-duration: .01ms !important; animation-iteration-count: 1 !important; transition-duration: .01ms !important; }
.obs-quality-low .obs-stage { backdrop-filter: none; box-shadow: none; }
.obs-roulette-wrapper { box-sizing: border-box; display: flex; align-items: center; justify-content: center; width: 100%; min-height: 100vh; padding: 24px; background-position: center; background-size: cover; }
.obs-animation-idle { visibility: hidden; opacity: 0; pointer-events: none; }
.obs-controls-enabled .obs-animation-idle { visibility: visible; opacity: 1; pointer-events: auto; }
.obs-animation-visible { animation: obs-stage-enter .35s ease-out both; }
.obs-stage { box-sizing: border-box; width: min(100%, 1040px); max-height: calc(100vh - 24px); overflow: auto; display: flex; flex-direction: column; align-items: center; gap: 16px; padding: 24px; border: 1px solid rgba(255,255,255,.14); border-radius: 32px; background: radial-gradient(circle at 50% 0%, rgba(255,255,255,.12), transparent 28%), var(--obs-accent-gradient), linear-gradient(180deg, rgba(12,18,34,.78), rgba(12,18,34,.52)); box-shadow: 0 28px 72px rgba(0,0,0,.35); backdrop-filter: blur(10px); }
.roulette-container { display: flex; flex-direction: column; align-items: center; position: relative; gap: 10px; cursor: pointer; }
.roulette-pointer { z-index: 2; margin-bottom: -10px; font-size: 2rem; text-align: center; }
.roulette-pointer img { width: 40px; }
.default-pointer { color: #ffcb47; font-size: 2.5rem; text-shadow: 0 0 8px rgba(255,203,71,.8); }
.roulette-wheel-wrapper { width: min(64vw, 52vh, 520px); aspect-ratio: 1; overflow: hidden; position: relative; z-index: 1; border-radius: 50%; box-shadow: 0 0 0 10px rgba(255,255,255,.06), 0 24px 54px rgba(0,0,0,.36); }
.roulette-wheel { width: 100%; height: 100%; transition-property: transform; transition-timing-function: cubic-bezier(.17,.67,.12,.99); }
.winner-segment { filter: brightness(1.4); stroke: #ffcb47 !important; stroke-width: 4 !important; animation: winner-pulse .9s ease-in-out 1; }
.roulette-spinning-aura { position: absolute; inset: -24px; border-radius: 50%; pointer-events: none; background: radial-gradient(circle, rgba(255,192,66,.24), rgba(255,192,66,.06) 34%, transparent 66%); animation: spin-aura 1s ease-in-out 1; }
.obs-win-result { z-index: 1; display: inline-flex; align-items: baseline; gap: 10px; max-width: 100%; padding: 12px 18px; border: 1px solid rgba(255,255,255,.12); border-radius: 999px; background: rgba(255,255,255,.08); box-shadow: 0 14px 34px rgba(0,0,0,.2); color: #fff; text-align: center; overflow-wrap: anywhere; }
.win-label { opacity: .8; }
.win-title { color: #ffcb47; font-size: 1.6rem; font-weight: bold; text-shadow: 0 0 12px rgba(255,203,71,.8); animation: result-pop .35s ease-out; }
.miss-result .win-title { color: #cbd5e1; text-shadow: none; }
.obs-controls { display: flex; flex-wrap: wrap; justify-content: center; align-items: center; gap: 10px; padding: 14px 16px; border: 1px solid rgba(255,255,255,.14); border-radius: 24px; background: rgba(255,255,255,.06); }
.obs-controls p { flex-basis: 100%; margin: 0; text-align: center; }
.obs-spin-btn, .obs-record-btn { border: 0; border-radius: 999px; padding: 12px 20px; font-size: 1rem; font-weight: 800; cursor: pointer; }
.obs-spin-btn { color: #18121a; background: linear-gradient(135deg,#ffcb47,#ff9f1a); }
.obs-record-btn { color: #fff; background: linear-gradient(135deg,#41c97d,#20935d); }
.obs-spin-btn:disabled, .obs-record-btn:disabled { cursor: not-allowed; opacity: .6; }
.obs-state { max-width: min(520px, calc(100vw - 32px)); margin: 16px; padding: 20px 24px; border: 1px solid rgba(248,113,113,.4); border-radius: 14px; background: rgba(15,23,42,.9); text-align: center; box-shadow: 0 18px 48px rgba(0,0,0,.28); }
.obs-state p { margin: 8px 0; }
.obs-state button { margin-top: 8px; padding: 8px 14px; border: 0; border-radius: 8px; cursor: pointer; }
.obs-reconnect-notice { position: fixed; z-index: 10; left: 50%; bottom: 24px; transform: translateX(-50%); display: flex; flex-wrap: wrap; align-items: center; justify-content: center; gap: 10px; max-width: calc(100vw - 32px); padding: 10px 14px; border: 1px solid rgba(248,113,113,.45); border-radius: 12px; background: rgba(15,23,42,.94); }
.obs-reconnect-notice button { padding: 8px 14px; border: 0; border-radius: 8px; cursor: pointer; }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0,0,0,0); white-space: nowrap; border: 0; }
@keyframes obs-stage-enter { from { opacity: 0; transform: scale(.98); } to { opacity: 1; transform: scale(1); } }
@keyframes winner-pulse { 0%,100% { filter: brightness(1.3); } 50% { filter: brightness(1.7); } }
@keyframes result-pop { from { transform: scale(.96); opacity: .4; } to { transform: scale(1); opacity: 1; } }
@keyframes spin-aura { 0%,100% { transform: scale(.98); opacity: .7; } 50% { transform: scale(1.04); opacity: 1; } }
</style>
