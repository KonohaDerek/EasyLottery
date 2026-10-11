<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { HubConnectionBuilder, type HubConnection } from "@microsoft/signalr";
import { useRoute } from "vue-router";
import { ApiError, apiRequest } from "../api/client";
import { readObsSessionToken } from "../obs/session-token";
import { reconnectLiveDraw } from "../obs/reconnect";
import type { PokeCell, PokeTemplate } from "../pokebox/template";
import { canApplyPokeLiveDrawState, canApplyPokeTemplateSnapshot, normalizeLiveDrawPhase, type LiveDrawPhase, type PokeLiveDrawState } from "../pokebox/live-draw";

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
const template = ref<PokeTemplate | null>(null);
const error = ref("");
const statusMessage = ref("");
const statusIsError = ref(false);
const connectionStatus = ref("連線中…");
const canRetryConnection = ref(false);
const retryingConnection = ref(false);
const flowPhase = ref<LiveDrawPhase>(0);
const countdownRemaining = ref<number | null>(null);
const lastPokedIndex = ref<number | null>(null);
const isRecording = ref(false);
const lastRecordedSignature = ref("");
const activePreset = ref("arcade-stage");
const accentGradient = ref("linear-gradient(135deg, #ffcb47, #ff9f1a)");
let appliedRevision = -1;
let templateRequestGeneration = 0;
let connection: HubConnection | null = null;
let countdownTimer: number | undefined;
let audioContext: AudioContext | null = null;

const isBusy = computed(() => flowPhase.value === 1 || flowPhase.value === 2);
const isDrawInProgress = computed(() => isBusy.value || flowPhase.value === 3);
const isIdle = computed(() => flowPhase.value === 0 && lastPokedIndex.value === null);
const revealedCount = computed(() => template.value?.cells.filter(cell => cell.isRevealed).length ?? 0);
const canRecord = computed(() => !!template.value && revealedCount.value > 0 && !isDrawInProgress.value && buildRecordSignature() !== lastRecordedSignature.value);
const orderedCells = computed(() => [...(template.value?.cells ?? [])].sort((a, b) => a.index - b.index));
const wrapperStyle = computed(() => ({
  "--obs-accent-gradient": accentGradient.value,
  backgroundImage: template.value?.backgroundImageUrl ? `url(${JSON.stringify(template.value.backgroundImageUrl)})` : undefined,
  fontFamily: template.value?.fontFamily || undefined
}));
const gridStyle = computed(() => ({
  gridTemplateColumns: `repeat(${template.value?.gridColumns ?? 3}, minmax(0, 1fr))`
}));
const animationClass = computed(() => ["anim-burst", "anim-smoke", "anim-flash", "anim-bounce", "anim-flip"][template.value?.animation ?? 0] ?? "anim-burst");
const flowDetailText = computed(() => {
  if (flowPhase.value === 0) return "選好格子後，畫面會依序進入倒數、揭露與結果階段。";
  if (flowPhase.value === 1) return "準備揭露，請留意接下來的動畫節奏。";
  if (flowPhase.value === 2) return "戳戳樂正在揭露中。";
  return revealedCount.value >= (template.value?.cells.length ?? 0)
    ? "本次戳戳樂已全部揭露完成。"
    : "結果已顯示，還可以繼續揭露下一格。";
});

const layoutGradients: Record<string, string> = {
  "stage-spotlight": "linear-gradient(135deg, #ffcb47, #ff9f1a)",
  "neon-panel": "linear-gradient(135deg, #63d4ff, #8b5cf6)",
  "festival-pane": "linear-gradient(135deg, #ffd36c, #ef6b6b)"
};
const cues: Record<string, { countdown: string; poke: string; reveal: string }> = {
  "arcade-stage": { countdown: "tone:784:140:square", poke: "tone:988:180:triangle", reveal: "tone:1046:240:triangle" },
  "soft-stage": { countdown: "tone:523:160:sine", poke: "tone:659:220:triangle", reveal: "tone:740:260:triangle" },
  "retro-pixel": { countdown: "tone:880:100:square", poke: "tone:1175:160:square", reveal: "tone:1319:220:triangle" }
};

function friendlyError(cause: unknown): string {
  if (cause instanceof ApiError && [401, 403].includes(cause.status)) return "OBS 工作階段無效或已過期，請回管理頁重新開啟預覽。";
  if (cause instanceof ApiError && cause.status === 429) return "請求過於頻繁，稍候再試。";
  return cause instanceof Error ? cause.message : "無法載入戳戳樂 OBS 畫面。";
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
  const tick = () => { countdownRemaining.value = Math.max(1, Math.ceil((Date.parse(endsAt) - Date.now()) / 1000)); };
  tick();
  countdownTimer = window.setInterval(tick, 200);
}

async function refreshTemplate(): Promise<void> {
  const requestGeneration = ++templateRequestGeneration;
  const requestRevision = appliedRevision;
  const nextTemplate = (await apiRequest<PokeTemplate>(`/api/poke-templates/public/${publicId}`)).data;
  if (requestGeneration !== templateRequestGeneration) return;
  if (appliedRevision > requestRevision) {
    await refreshTemplate();
    return;
  }
  if (canApplyPokeTemplateSnapshot(requestGeneration, templateRequestGeneration, requestRevision, appliedRevision)) {
    template.value = nextTemplate;
  }
}

async function applyLiveState(state: PokeLiveDrawState): Promise<void> {
  if (!canApplyPokeLiveDrawState(state, publicId, appliedRevision) || state.revision === appliedRevision) return;
  const previousRevision = appliedRevision;
  appliedRevision = state.revision;
  flowPhase.value = normalizeLiveDrawPhase(state.phase);
  updateCountdown(state.phaseEndsAtUtc);

  if (state.pokeCellIndex !== null && template.value) {
    const cell = template.value.cells.find(item => item.index === state.pokeCellIndex);
    if (cell) {
      cell.isRevealed = true;
      cell.revealedAt ??= state.updatedAtUtc;
      lastPokedIndex.value = cell.index;
    }
  } else if (flowPhase.value === 0 && state.revision > 1) {
    lastPokedIndex.value = null;
    try {
      await refreshTemplate();
    } catch (cause) {
      statusMessage.value = friendlyError(cause);
      statusIsError.value = true;
    }
  }

  const preset = cues[activePreset.value] ?? cues["arcade-stage"];
  if (flowPhase.value === 1) void playCue(preset.countdown);
  if (flowPhase.value === 2) void playCue(preset.poke);
  if (flowPhase.value === 3) void playCue(preset.reveal);

  if (previousRevision !== appliedRevision) statusIsError.value = false;
}

async function readSessionToken(): Promise<string> {
  const token = readObsSessionToken();
  if (!token) throw new Error("OBS 工作階段 token 不存在，請從管理頁重新開啟預覽。");
  return token;
}

async function refreshLiveState(): Promise<void> {
  try {
    const state = (await apiRequest<PokeLiveDrawState>(`/api/live-draw/pokebox/${publicId}`)).data;
    await applyLiveState(state);
  } catch (cause) {
    if (cause instanceof ApiError && cause.status === 404) {
      await applyLiveState({ publicId, revision: appliedRevision + 1, phase: 0, phaseEndsAtUtc: null, pokeCellIndex: null, resultTitle: null, updatedAtUtc: new Date().toISOString() });
      return;
    }
    throw cause;
  }
}

async function joinAndRefresh(): Promise<void> {
  if (!connection) return;
  await connection.invoke("JoinSession", "pokebox", publicId, await readSessionToken());
  await refreshTemplate();
  await refreshLiveState();
}

async function retryConnection(): Promise<void> {
  if (!connection || retryingConnection.value) return;
  retryingConnection.value = true;
  canRetryConnection.value = false;
  connectionStatus.value = "正在重新連線…";
  try {
    await reconnectLiveDraw(connection, () => {
      appliedRevision = -1;
      lastPokedIndex.value = null;
      templateRequestGeneration++;
    }, joinAndRefresh);
    connectionStatus.value = "即時連線已恢復";
    error.value = "";
    statusMessage.value = "";
  } catch (cause) {
    canRetryConnection.value = true;
    connectionStatus.value = "重新連線失敗，請再試一次";
    statusMessage.value = friendlyError(cause);
    statusIsError.value = true;
  } finally {
    retryingConnection.value = false;
  }
}

async function connect(): Promise<void> {
  connectionStatus.value = "連線中…";
  const next = new HubConnectionBuilder().withUrl("/hubs/live-draw").withAutomaticReconnect().build();
  connection = next;
  next.on("LiveDrawStateChanged", (state: PokeLiveDrawState) => { void applyLiveState(state); });
  next.onreconnecting(() => {
    canRetryConnection.value = false;
    connectionStatus.value = "即時連線中斷，正在重新連線…";
  });
  next.onreconnected(async () => {
    try {
      // The server keeps phase state in memory; a new snapshot is authoritative after restart.
      appliedRevision = -1;
      lastPokedIndex.value = null;
      templateRequestGeneration++;
      await joinAndRefresh();
      connectionStatus.value = "即時連線已恢復";
      canRetryConnection.value = false;
      error.value = "";
    } catch (cause) {
      connectionStatus.value = "即時連線已恢復，但重新加入失敗";
      statusMessage.value = friendlyError(cause);
      statusIsError.value = true;
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
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(publicId)) throw new Error("戳戳樂 OBS 網址無效。");
    await refreshTemplate();
    await loadSettings();
    await connect();
  } catch (cause) {
    error.value = friendlyError(cause);
    connectionStatus.value = "無法建立即時連線";
  } finally {
    loading.value = false;
  }
}

function pokeCell(cellIndex: number | null): void {
  if (!template.value || isDrawInProgress.value || !controlsEnabled.value) return;
  if (cellIndex !== null) {
    const cell = template.value.cells.find(item => item.index === cellIndex);
    if (!cell || (cell.isRevealed && !template.value.allowRePoking && template.value.mode !== 0)) return;
  }
  statusMessage.value = "";
  void apiRequest<PokeLiveDrawState>(`/api/live-draw/pokebox/${publicId}/poke`, {
    method: "POST",
    body: JSON.stringify({ cellIndex, countdownSeconds: 2 })
  }).then(response => applyLiveState(response.data)).catch(cause => {
    statusMessage.value = friendlyError(cause);
    statusIsError.value = true;
  });
}

function onCellClick(cell: PokeCell): void {
  if (template.value?.mode === 0) pokeCell(null);
  else pokeCell(cell.index);
}

function cellStyle(cell: PokeCell): Record<string, string> {
  const color = cell.revealedColor || "#cccccc";
  return {
    ...(cell.isRevealed ? { background: color, borderColor: color } : {}),
    "--reveal-duration": `${Math.max(300, Math.min(10000, template.value?.animationDurationMs ?? 650))}ms`
  };
}

function cellLabel(cell: PokeCell): string {
  return cell.isRevealed ? `第 ${cell.index + 1} 格，已揭露：${cell.title}` : `第 ${cell.index + 1} 格，尚未揭露`;
}

function buildRecordSignature(): string {
  if (!template.value) return "";
  return template.value.cells.filter(cell => cell.isRevealed).sort((a, b) => a.index - b.index)
    .map(cell => `${cell.id}:${cell.revealedAt ? Date.parse(cell.revealedAt) : 0}`).join("|");
}

async function recordResult(): Promise<void> {
  if (!template.value || !canRecord.value || isRecording.value) return;
  isRecording.value = true;
  try {
    const record = (await apiRequest<ActivityRecord>(`/api/poke-templates/public/${publicId}/record-result`, { method: "POST" })).data;
    lastRecordedSignature.value = buildRecordSignature();
    const details = record.items.length
      ? `\n\n結果明細：\n${[...record.items].sort((a, b) => a.order - b.order).map(item => `${item.order}. ${item.name} - ${item.description}`).join("\n")}`
      : "";
    void apiRequest("/api/result-notification", {
      method: "POST",
      body: JSON.stringify({
        subject: `EasyLottery 結果通知 - ${record.activityName}`,
        body: `EasyLottery 結果通知\n活動名稱：${record.activityName}\n活動時間：${new Intl.DateTimeFormat("zh-TW", { year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", second: "2-digit", hourCycle: "h23" }).format(new Date(record.activityDateUtc))}\n摘要：${record.summary}${details}`,
        resourceKind: "pokebox",
        resourceId: record.templatePublicId
      })
    }).catch(() => undefined);
    statusMessage.value = `已記錄活動「${record.activityName}」結果。`;
    statusIsError.value = false;
  } catch (cause) {
    statusMessage.value = friendlyError(cause);
    statusIsError.value = true;
  } finally {
    isRecording.value = false;
  }
}

async function resetTemplate(): Promise<void> {
  if (!template.value || isDrawInProgress.value) return;
  statusMessage.value = "";
  try {
    const state = (await apiRequest<PokeLiveDrawState>(`/api/live-draw/pokebox/${publicId}/reset`, { method: "POST" })).data;
    lastRecordedSignature.value = "";
    await applyLiveState(state);
    statusMessage.value = "已重設戳戳樂。";
    statusIsError.value = false;
  } catch (cause) {
    statusMessage.value = friendlyError(cause);
    statusIsError.value = true;
  }
}

function reload(): void { window.location.reload(); }

onMounted(() => { void initialize(); });
onBeforeUnmount(() => {
  window.clearInterval(countdownTimer);
  if (connection) void connection.stop();
  if (audioContext) void audioContext.close();
});
</script>

<template>
  <main class="obs-layout" :class="{ 'obs-controls-enabled': controlsEnabled, 'obs-motion-reduced': motionReduced, 'obs-quality-low': lowQuality }">
    <div v-if="loading" class="sr-only" role="status">正在載入戳戳樂 OBS 畫面…</div>
    <div v-else-if="error && controlsEnabled" class="obs-state obs-state-error" role="alert">
      <strong>無法載入戳戳樂 OBS</strong>
      <p>{{ error }}</p>
      <button type="button" @click="reload">重新載入</button>
    </div>
    <div v-else-if="error" class="sr-only" role="status">{{ error }}</div>
    <template v-else-if="template">
      <div class="obs-poke-wrapper" :class="isIdle ? 'obs-animation-idle' : 'obs-animation-visible'" :style="wrapperStyle">
        <section class="obs-stage">
          <div class="live-draw-flow-guide" role="status" aria-live="polite" aria-atomic="true">
            <div class="flow-eyebrow">LIVE DRAW FLOW</div>
            <div class="flow-title">戳戳樂流程</div>
            <div class="flow-subtitle">{{ template.name }}</div>
            <div class="flow-steps">
              <span v-for="(label, index) in ['倒數準備', '戳開動畫', '結果揭曉']" :key="label" class="flow-step" :class="{ active: flowPhase === index + 1, completed: flowPhase > index + 1 }">
                <b>{{ index + 1 }}</b>{{ label }}
              </span>
            </div>
            <div v-if="countdownRemaining !== null" class="flow-countdown" aria-live="assertive">{{ countdownRemaining }}</div>
            <div class="flow-detail">{{ flowDetailText }}</div>
          </div>

          <div class="poke-grid" :style="gridStyle" aria-label="戳戳樂格子">
            <button
              v-for="cell in orderedCells"
              :key="cell.id || cell.index"
              type="button"
              class="poke-cell"
              :class="[{ revealed: cell.isRevealed, 'just-poked': lastPokedIndex === cell.index }, cell.isRevealed ? animationClass : '']"
              :style="cellStyle(cell)"
              :aria-label="cellLabel(cell)"
              :disabled="!controlsEnabled || isDrawInProgress || (cell.isRevealed && !template.allowRePoking && template.mode !== 0)"
              @click="onCellClick(cell)"
            >
              <template v-if="cell.isRevealed">
                <img v-if="cell.revealedImageUrl" :src="cell.revealedImageUrl" :alt="cell.title" />
                <div class="cell-title">{{ cell.title }}</div>
                <div v-if="cell.subTitle" class="cell-subtitle">{{ cell.subTitle }}</div>
              </template>
              <template v-else>
                <img v-if="cell.imageUrl" :src="cell.imageUrl" :alt="cell.title" />
                <span v-else class="poke-icon" aria-hidden="true">👆</span>
              </template>
            </button>
          </div>

          <div v-if="controlsEnabled" class="obs-controls">
            <button v-if="template.mode === 0" class="obs-poke-btn" type="button" :disabled="isDrawInProgress" @click="pokeCell(null)">隨機戳一格</button>
            <button class="obs-record-btn" type="button" :disabled="!canRecord || isRecording" @click="recordResult">{{ isRecording ? "記錄中…" : "結束本次活動並記錄" }}</button>
            <button class="obs-reset-btn" type="button" :disabled="isDrawInProgress" @click="resetTemplate">重設</button>
            <p role="status" aria-live="polite">{{ connectionStatus }}</p>
            <p v-if="statusMessage" :role="statusIsError ? 'alert' : 'status'" aria-live="polite">{{ statusMessage }}</p>
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
:global(html), :global(body), :global(#app) { width: 100%; min-height: 100%; margin: 0; background: transparent !important; }
.obs-layout { display: flex; width: 100vw; min-height: 100vh; align-items: center; justify-content: center; overflow: hidden; color: #fff; }
.obs-poke-wrapper { box-sizing: border-box; display: flex; width: 100%; min-height: 100vh; align-items: center; justify-content: center; padding: 24px; background-position: center; background-size: cover; }
.obs-animation-idle { visibility: hidden; opacity: 0; pointer-events: none; }
.obs-controls-enabled .obs-animation-idle { visibility: visible; opacity: 1; pointer-events: auto; }
.obs-animation-visible { animation: obs-stage-enter .35s ease-out both; }
.obs-stage { box-sizing: border-box; display: flex; width: min(100%, 980px); max-height: calc(100vh - 24px); flex-direction: column; align-items: center; gap: 16px; overflow: auto; padding: 24px; border: 1px solid rgba(255,255,255,.14); border-radius: 32px; background: radial-gradient(circle at 50% 0%, rgba(255,255,255,.12), transparent 28%), var(--obs-accent-gradient), linear-gradient(180deg, rgba(12,18,34,.78), rgba(12,18,34,.52)); box-shadow: 0 28px 72px rgba(0,0,0,.35); backdrop-filter: blur(10px); }
.live-draw-flow-guide { box-sizing: border-box; width: 100%; padding: 14px 16px; border-radius: 16px; background: rgba(15,23,42,.78); color: #fff; box-shadow: 0 18px 48px rgba(0,0,0,.22); }
.flow-eyebrow { font-size: .72rem; font-weight: 700; letter-spacing: .18em; opacity: .7; }
.flow-title { margin-top: 4px; font-size: 1.08rem; font-weight: 800; }
.flow-subtitle, .flow-detail { font-size: .92rem; line-height: 1.5; opacity: .88; }
.flow-steps { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 12px; }
.flow-step { display: inline-flex; align-items: center; gap: 8px; padding: 8px 12px; border: 1px solid rgba(255,255,255,.12); border-radius: 999px; background: rgba(255,255,255,.08); font-size: .88rem; font-weight: 700; opacity: .74; }
.flow-step.active { border-color: rgba(255,255,255,.22); background: rgba(255,255,255,.16); opacity: 1; }
.flow-step.completed { border-color: rgba(74,222,128,.28); background: rgba(74,222,128,.18); opacity: .92; }
.flow-step b { display: inline-flex; width: 22px; height: 22px; align-items: center; justify-content: center; border-radius: 50%; background: rgba(255,255,255,.14); font-size: .78rem; }
.flow-countdown { margin-top: 12px; padding: 10px; border-radius: 14px; background: linear-gradient(135deg,rgba(234,179,8,.22),rgba(59,130,246,.14)); text-align: center; font-size: 3rem; font-weight: 900; line-height: 1; }
.flow-detail { margin-top: 10px; }
.poke-grid { display: grid; width: 100%; max-width: 760px; gap: 8px; }
.poke-cell { position: relative; display: flex; width: 100%; min-width: 0; min-height: 80px; flex-direction: column; align-items: center; justify-content: center; overflow: hidden; padding: 4px; border: 2px solid rgba(255,255,255,.28); border-radius: 8px; background: rgba(255,255,255,.16); color: #fff; font: inherit; font-size: .85rem; text-align: center; user-select: none; cursor: pointer; transition: transform .2s, box-shadow .2s; }
.poke-cell:disabled { cursor: default; }
.poke-cell:hover:not(:disabled):not(.revealed) { transform: scale(1.05); box-shadow: 0 0 12px rgba(255,203,71,.8); }
.poke-cell img { max-width: 100%; max-height: 80%; object-fit: contain; }
.poke-cell.revealed { color: #18121a; box-shadow: 0 0 0 2px rgba(255,255,255,.08) inset, 0 12px 26px rgba(0,0,0,.18); animation: reveal-glow .7s ease-out; }
.poke-cell.revealed::before { position: absolute; inset: 0; border-radius: inherit; background: linear-gradient(135deg,rgba(255,255,255,.24),transparent 38%,rgba(255,255,255,.08)); content: ""; pointer-events: none; animation: reveal-sheen 1.1s ease-out 1; mix-blend-mode: screen; }
.poke-cell.revealed::after { position: absolute; top: 8px; right: 10px; color: rgba(255,255,255,.9); content: "✦"; font-size: .9rem; text-shadow: 0 0 10px rgba(255,255,255,.5); pointer-events: none; animation: reveal-star 1s ease-in-out 1; }
.poke-cell .poke-icon { font-size: 1.8rem; }
.poke-cell .cell-title { margin-top: 4px; font-weight: 800; }
.poke-cell .cell-subtitle { font-size: .75rem; opacity: .8; }
.poke-cell.just-poked.anim-burst { animation: burst var(--reveal-duration,650ms) ease-out; }
.poke-cell.just-poked.anim-bounce { animation: bounce var(--reveal-duration,650ms) ease-out; }
.poke-cell.just-poked.anim-flash { animation: flash var(--reveal-duration,650ms) ease-out; }
.poke-cell.just-poked.anim-smoke { animation: smoke var(--reveal-duration,650ms) ease-out; }
.poke-cell.just-poked.anim-flip { animation: flip var(--reveal-duration,650ms) ease-out; }
.obs-controls { display: flex; flex-wrap: wrap; align-items: center; justify-content: center; gap: 10px; padding: 14px 16px; border: 1px solid rgba(255,255,255,.14); border-radius: 20px; background: rgba(255,255,255,.06); }
.obs-controls p { margin: 0; }
.obs-poke-btn, .obs-record-btn, .obs-reset-btn, .obs-state button, .obs-reconnect-notice button { padding: 10px 16px; border: 0; border-radius: 999px; font: inherit; font-weight: 800; cursor: pointer; }
.obs-poke-btn { background: linear-gradient(135deg,#ffcb47,#ff9f1a); color: #18121a; }
.obs-record-btn { background: linear-gradient(135deg,#41c97d,#20935d); color: #fff; }
.obs-reset-btn { background: linear-gradient(135deg,#ef6b6b,#c53e3e); color: #fff; }
.obs-controls button:disabled { cursor: not-allowed; opacity: .6; }
.obs-state { max-width: min(520px,calc(100vw - 32px)); margin: 16px; padding: 20px 24px; border: 1px solid rgba(248,113,113,.4); border-radius: 14px; background: rgba(15,23,42,.9); text-align: center; box-shadow: 0 18px 48px rgba(0,0,0,.28); }
.obs-state p { margin: 8px 0; }
.obs-reconnect-notice { position: fixed; z-index: 10; bottom: 24px; left: 50%; display: flex; max-width: calc(100vw - 32px); flex-wrap: wrap; align-items: center; justify-content: center; gap: 10px; padding: 10px 14px; transform: translateX(-50%); border: 1px solid rgba(248,113,113,.45); border-radius: 12px; background: rgba(15,23,42,.94); }
.obs-motion-reduced *, .obs-motion-reduced *::before, .obs-motion-reduced *::after, .obs-quality-low .obs-stage { animation-duration: .01ms !important; animation-iteration-count: 1 !important; transition-duration: .01ms !important; }
.obs-quality-low .obs-stage { backdrop-filter: none; box-shadow: none; }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0,0,0,0); white-space: nowrap; }
@media (max-width: 640px) { .obs-poke-wrapper { padding: 12px; } .obs-stage { gap: 10px; padding: 12px; border-radius: 18px; } .flow-step { padding: 6px 8px; font-size: .78rem; } .poke-cell { min-height: 56px; font-size: .75rem; } }
@media (prefers-reduced-motion: reduce) { .obs-poke-wrapper, .obs-poke-wrapper *, .obs-poke-wrapper::before, .obs-poke-wrapper::after { animation: none !important; transition: none !important; } }
@keyframes obs-stage-enter { from { opacity: 0; transform: scale(.98); } to { opacity: 1; transform: scale(1); } }
@keyframes burst { 0% { transform: scale(1); } 30% { transform: scale(1.4); } 60% { transform: scale(.9); } 100% { transform: scale(1); } }
@keyframes bounce { 0%,100% { transform: translateY(0); } 30% { transform: translateY(-16px); } 60% { transform: translateY(-8px); } }
@keyframes flash { 0%,100% { opacity: 1; box-shadow: none; } 50% { opacity: .5; box-shadow: 0 0 24px 8px rgba(255,203,71,.7); } }
@keyframes smoke { 0%,100% { opacity: 1; filter: blur(0); } 50% { opacity: .4; filter: blur(4px); } }
@keyframes flip { 0%,100% { transform: rotateY(0); } 50% { transform: rotateY(90deg); } }
@keyframes reveal-glow { 0% { transform: scale(.96); filter: brightness(.9); } 50% { transform: scale(1.02); filter: brightness(1.18); } 100% { transform: scale(1); filter: brightness(1); } }
@keyframes reveal-sheen { 0% { opacity: .1; transform: translateX(-6px); } 50% { opacity: .8; transform: translateX(6px); } 100% { opacity: .12; transform: translateX(-6px); } }
@keyframes reveal-star { 0%,100% { transform: scale(.85) rotate(0); opacity: .5; } 50% { transform: scale(1.15) rotate(12deg); opacity: 1; } }
</style>
