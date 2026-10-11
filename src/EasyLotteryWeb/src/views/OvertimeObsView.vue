<script setup lang="ts">
import { HubConnectionBuilder, HubConnectionState, type HubConnection } from "@microsoft/signalr";
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { ApiError } from "../api/client";
import { appendOvertimeEvent, readOvertimeFeed, readOvertimeSettings, saveOvertimeSettings, type OvertimeOverlaySettings, type OvertimeSupportEvent } from "../overtime/api";
import { calculateProgressPercent, extendEndForPause, getDisplaySessionState, getRewardSummary, isEventVisible, OvertimeSessionRevision, remainingTimeText } from "../overtime/overlay";
import { readObsSessionToken } from "../obs/session-token";
import { createOvertimeHubLifecycleCallbacks } from "../overtime/realtime";
import "../overtime/overtime.css";

const route = useRoute();
const controlsEnabled = computed(() => route.query.controls === "1");
const loading = ref(true);
const busy = ref(false);
const settings = ref<OvertimeOverlaySettings | null>(null);
const events = ref<OvertimeSupportEvent[]>([]);
const error = ref("");
const connectionStatus = ref("連線中…");
const clock = ref(new Date());
const fireworksStartedAt = ref<number | null>(null);
const snapshotRevision = new OvertimeSessionRevision();
let connection: HubConnection | null = null;
let timer: number | undefined;
let ticksSinceRefresh = 0;
let completionSaveAttempted = false;

const state = computed(() => getDisplaySessionState(settings.value ?? {}, clock.value));
const plannedEndMarker = computed(() => events.value.find(event => event.source === 3 && event.sourceLabel?.toLocaleLowerCase() === "預計關台時間"));
const sessionEndAtUtc = computed(() => settings.value?.plannedEndAtUtc
  ?? events.value.find(event => event.sessionEndAtUtc)?.sessionEndAtUtc
  ?? plannedEndMarker.value?.sessionEndAtUtc
  ?? null);
const plannedEndForStart = computed(() => plannedEndMarker.value?.sessionEndAtUtc ?? settings.value?.plannedEndAtUtc ?? null);
const sessionClock = computed(() => state.value === "paused" && settings.value?.pausedAtUtc
  ? new Date(settings.value.pausedAtUtc)
  : clock.value);
const remaining = computed(() => remainingTimeText(sessionEndAtUtc.value, sessionClock.value));
const progress = computed(() => calculateProgressPercent(settings.value?.streamStartedAtUtc, sessionEndAtUtc.value, sessionClock.value));
const canStart = computed(() => {
  const plannedEnd = plannedEndForStart.value ? Date.parse(plannedEndForStart.value) : Number.NaN;
  return Number.isFinite(plannedEnd) && plannedEnd > clock.value.getTime();
});
const visibleEvent = computed(() => events.value.find(event =>
  event.source !== 3 && isEventVisible(event.occurredAtUtc, clock.value, settings.value?.supportMessageVisibleSeconds ?? 8)
) ?? null);
const messageAnimationClass = computed(() => {
  switch (settings.value?.textAnimationKey?.trim().toLocaleLowerCase()) {
    case "fade-in": return "animation-fade-in";
    case "pop-in": return "animation-pop-in";
    case "none": return "";
    default: return "animation-slide-in";
  }
});
const fireworksDuration = computed(() => Math.min(15, Math.max(2, settings.value?.completionFireworksDurationSeconds ?? 6)));
const showFireworks = computed(() => settings.value?.enableCompletionFireworks !== false
  && fireworksStartedAt.value !== null
  && clock.value.getTime() - fireworksStartedAt.value < fireworksDuration.value * 1000);

watch(state, (next, previous) => {
  if (previous === "running" && next === "completed") {
    if (settings.value?.enableCompletionFireworks !== false) fireworksStartedAt.value = clock.value.getTime();
  }
  if (next === "completed" && controlsEnabled.value && settings.value?.sessionState === "running" && !completionSaveAttempted) {
    completionSaveAttempted = true;
    void completeExpiredSession();
  }
  if (next !== "completed") {
    completionSaveAttempted = false;
  }
});

function safeError(cause: unknown): string {
  if (cause instanceof ApiError && cause.status === 403) return "OBS 權限不足（HTTP 403），此工作階段不能控制加班台。";
  if (cause instanceof ApiError && cause.status === 401) return "OBS 工作階段無效或已過期（HTTP 401），請重新開啟預覽。";
  return cause instanceof Error ? cause.message : "加班台 OBS 操作失敗。";
}

async function refreshSnapshot(): Promise<void> {
  const revision = snapshotRevision.begin();
  try {
    const [nextSettings, nextEvents] = await Promise.all([readOvertimeSettings(), readOvertimeFeed()]);
    if (!snapshotRevision.isCurrent(revision)) return;
    settings.value = nextSettings;
    events.value = nextEvents;
    error.value = "";
  } catch (cause) {
    if (snapshotRevision.isCurrent(revision)) error.value = safeError(cause);
  } finally {
    if (snapshotRevision.isCurrent(revision)) loading.value = false;
  }
}

async function joinAndRefresh(hub: HubConnection, token: string): Promise<void> {
  await hub.invoke("Join", token);
  connectionStatus.value = "即時連線中";
  await refreshSnapshot();
}

async function startRealtime(): Promise<void> {
  const token = readObsSessionToken();
  if (!token) {
    connectionStatus.value = "未提供 OBS 工作階段";
    return;
  }

  const hub = new HubConnectionBuilder()
    .withUrl("/hubs/overtime")
    .withAutomaticReconnect()
    .build();
  connection = hub;
  hub.on("OvertimeStateChanged", () => { void refreshSnapshot(); });
  hub.on("OvertimeFeedChanged", () => { void refreshSnapshot(); });
  const lifecycle = createOvertimeHubLifecycleCallbacks(
    (method, value) => hub.invoke(method, value),
    token,
    status => { connectionStatus.value = status; },
    refreshSnapshot,
    cause => { error.value = safeError(cause); }
  );
  hub.onreconnecting(lifecycle.onreconnecting);
  hub.onreconnected(() => { void lifecycle.onreconnected(); });
  hub.onclose(lifecycle.onclose);

  try {
    await hub.start();
    await joinAndRefresh(hub, token);
  } catch {
    connectionStatus.value = "即時連線中斷，改用輪詢";
  }
}

async function persistSettings(next: OvertimeOverlaySettings): Promise<void> {
  error.value = "";
  await saveOvertimeSettings(next);
  await refreshSnapshot();
}

async function runAction(action: () => Promise<void>): Promise<void> {
  if (busy.value) return;
  busy.value = true;
  error.value = "";
  try {
    await action();
  } catch (cause) {
    error.value = safeError(cause);
  } finally {
    busy.value = false;
  }
}

function makeManualEvent(sourceLabel: string, message: string, sessionEndAtUtc: string | null): OvertimeSupportEvent {
  return {
    source: 3,
    sourceLabel,
    displayName: sourceLabel,
    message,
    amount: null,
    amountDisplay: "",
    currency: "TWD",
    avatarUrl: "",
    color: "#8bd3ff",
    occurredAtUtc: new Date().toISOString(),
    sessionEndAtUtc
  };
}

async function startCountdown(): Promise<void> {
  if (!settings.value || !canStart.value || !plannedEndForStart.value) return;
  const now = new Date().toISOString();
  const plannedEnd = plannedEndForStart.value;
  await persistSettings({
    ...settings.value,
    streamStartedAtUtc: now,
    plannedEndAtUtc: plannedEnd,
    pausedAtUtc: null,
    completedAtUtc: null,
    sessionState: "running"
  });
  await appendOvertimeEvent(makeManualEvent("加班開始", "加班台已開始。", plannedEnd));
  await refreshSnapshot();
}

async function pauseCountdown(): Promise<void> {
  if (!settings.value || state.value !== "running") return;
  await persistSettings({ ...settings.value, pausedAtUtc: new Date().toISOString(), sessionState: "paused" });
}

async function resumeCountdown(): Promise<void> {
  if (!settings.value || state.value !== "paused") return;
  const now = new Date().toISOString();
  await persistSettings({
    ...settings.value,
    plannedEndAtUtc: extendEndForPause(sessionEndAtUtc.value, settings.value.pausedAtUtc, now) ?? settings.value.plannedEndAtUtc,
    pausedAtUtc: null,
    sessionState: "running"
  });
}

async function stopCountdown(): Promise<void> {
  if (!settings.value) return;
  await persistSettings({ ...settings.value, pausedAtUtc: null, sessionState: "ended" });
  await appendOvertimeEvent(makeManualEvent("加班停止", "加班台已停止。", sessionEndAtUtc.value));
  await refreshSnapshot();
}

async function sendTestNotification(): Promise<void> {
  const amount = settings.value?.demoEcpayAmount && settings.value.demoEcpayAmount > 0
    ? settings.value.demoEcpayAmount
    : 100;
  await appendOvertimeEvent({
    source: 2,
    sourceLabel: "ECPay Donate",
    displayName: "測試贊助者",
    message: "加班台 OBS 測試通知",
    amount,
    amountDisplay: "NT$100",
    currency: "TWD",
    externalId: `obs-overtime-test:${crypto.randomUUID()}`,
    occurredAtUtc: new Date().toISOString()
  });
  await refreshSnapshot();
}

async function completeExpiredSession(): Promise<void> {
  if (!settings.value) return;
  try {
    await persistSettings({ ...settings.value, sessionState: "completed", completedAtUtc: new Date().toISOString() });
  } catch (cause) {
    error.value = safeError(cause);
  }
}

function buildMessage(event: OvertimeSupportEvent): string {
  if (event.message?.trim()) return event.message;
  if (event.source === 1) return "送出 SuperChat 支援。";
  if (event.source === 2) return "送出綠界 Donate 支援。";
  return "加班訊息";
}

function amountText(event: OvertimeSupportEvent): string {
  if (event.amountDisplay?.trim()) return event.amountDisplay;
  if (event.amount === null || event.amount === undefined) return "";
  return `${event.amount.toLocaleString("zh-TW", { maximumFractionDigits: 2 })} ${event.currency || "TWD"}`;
}

function avatarFallback(name: string): string {
  const value = Array.from(name.trim());
  return value.length ? value.slice(0, 2).join("") : "?";
}

function particleStyle(particle: number): Record<string, string> {
  const angle = particle * 30 * Math.PI / 180;
  const travelX = Math.cos(angle) * 160;
  const travelY = Math.sin(angle) * 160;
  const fallX = Math.cos(angle) * 200;
  const fallY = Math.sin(angle) * 200 + 45;
  return {
    "--travel-x": `${travelX.toFixed(2)}px`,
    "--travel-y": `${travelY.toFixed(2)}px`,
    "--fall-x": `${fallX.toFixed(2)}px`,
    "--fall-y": `${fallY.toFixed(2)}px`
  };
}

onMounted(async () => {
  await refreshSnapshot();
  await startRealtime();
  timer = window.setInterval(() => {
    clock.value = new Date();
    if (connection?.state !== HubConnectionState.Connected) {
      ticksSinceRefresh += 1;
      if (ticksSinceRefresh >= 3) {
        ticksSinceRefresh = 0;
        void refreshSnapshot();
      }
    }
  }, 1000);
});

onBeforeUnmount(() => {
  if (timer !== undefined) window.clearInterval(timer);
  if (connection) void connection.stop();
  connection = null;
});
</script>

<template>
  <main v-if="!loading && (state !== 'idle' && state !== 'ended' || controlsEnabled)" class="overtime-overlay" aria-label="加班台 OBS">
    <div class="overtime-shell">
      <div v-if="controlsEnabled" class="overtime-obs-controls">
        <div class="overtime-control-status" role="status">{{ connectionStatus }}</div>
        <div class="overtime-standby-actions">
          <button type="button" :disabled="busy" @click="runAction(sendTestNotification)">測試贊助通知</button>
          <button v-if="state !== 'running' && state !== 'paused'" type="button" :disabled="busy || !canStart" @click="runAction(startCountdown)">開始加班</button>
          <button v-else-if="state === 'running'" type="button" :disabled="busy" @click="runAction(pauseCountdown)">暫停加班</button>
          <button v-else type="button" :disabled="busy" @click="runAction(resumeCountdown)">繼續加班</button>
          <button type="button" :disabled="busy" @click="runAction(stopCountdown)">結束加班</button>
        </div>
      </div>

      <div class="overtime-minimal-overlay">
        <section class="overtime-progress-wrap" aria-label="加班進度">
          <div class="overtime-progress-panel">
            <div class="overtime-progress-header">
              <div class="overtime-progress-label">加班<br />時間</div>
              <div class="overtime-remaining-time" aria-live="off">{{ remaining }}</div>
            </div>
            <div class="overtime-progress-title">加班進度</div>
            <div class="overtime-progress-track" role="progressbar" aria-valuemin="0" aria-valuemax="100" :aria-valuenow="progress">
              <div class="overtime-progress-fill" :style="{ width: `${progress}%` }"></div>
              <div class="overtime-progress-value">{{ progress.toFixed(2) }}%</div>
            </div>
          </div>
        </section>

        <div v-if="visibleEvent" :key="visibleEvent.id || visibleEvent.externalId || visibleEvent.occurredAtUtc" class="overtime-live-line" :class="messageAnimationClass" :style="{ borderColor: visibleEvent.color || '#ff85b4' }">
          <img v-if="settings?.showAvatars !== false && visibleEvent.avatarUrl" class="overtime-live-avatar" :src="visibleEvent.avatarUrl" :alt="visibleEvent.displayName" />
          <span v-else-if="settings?.showAvatars !== false" class="overtime-live-avatar fallback">{{ avatarFallback(visibleEvent.displayName) }}</span>
          <span class="overtime-live-source">{{ visibleEvent.sourceLabel || "贊助" }}</span>
          <span class="overtime-live-divider">·</span>
          <span class="overtime-live-name">{{ visibleEvent.displayName }}</span>
          <span v-if="settings?.showAmounts !== false && amountText(visibleEvent)" class="overtime-live-divider">·</span>
          <span v-if="settings?.showAmounts !== false && amountText(visibleEvent)" class="overtime-live-amount">{{ amountText(visibleEvent) }}</span>
          <span v-if="getRewardSummary(settings?.rewardRules, visibleEvent.amount)" class="overtime-live-divider">·</span>
          <span v-if="getRewardSummary(settings?.rewardRules, visibleEvent.amount)" class="overtime-live-reward">{{ getRewardSummary(settings?.rewardRules, visibleEvent.amount) }}</span>
          <p v-if="buildMessage(visibleEvent)" class="overtime-live-message">{{ buildMessage(visibleEvent) }}</p>
        </div>
      </div>

      <div v-if="showFireworks" class="overtime-fireworks" :class="settings?.completionFireworksStyle || 'classic'" :style="{ '--fireworks-duration': `${fireworksDuration}s` }" aria-hidden="true">
        <img v-if="settings?.completionFireworksGifUrl" class="overtime-fireworks-gif" :src="settings.completionFireworksGifUrl" alt="" />
        <template v-else>
          <div v-for="burst in 4" :key="burst" class="overtime-firework-burst" :class="`burst-${burst - 1}`">
            <span v-for="particle in 12" :key="particle" class="overtime-firework-particle" :style="particleStyle(particle - 1)"></span>
          </div>
        </template>
      </div>
    </div>
  </main>
  <div v-if="error && (controlsEnabled || !settings)" class="overtime-error" role="alert">{{ error }}</div>
</template>
