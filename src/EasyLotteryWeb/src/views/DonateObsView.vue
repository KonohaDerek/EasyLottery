<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { ApiError } from "../api/client";
import { generateDonateCongratulation, readDonateSnapshot, sendTestDonation, type DonateActivity } from "../donate-obs/api";
import {
  acceptDonateSnapshot,
  advanceDonateReveal,
  completeDonateAnimation,
  createDonateObsState,
  shouldWaitForAnimationVideo,
  type DonateObsState,
} from "../donate-obs/lifecycle";
import "../donate-obs/donate-obs.css";

const route = useRoute();
const routeKey = computed(() => String(route.params.activityRouteKey ?? ""));
const controlsEnabled = computed(() => route.query.controls === "1");
const requestedMotionReduction = computed(() => route.query.motion === "reduce");
const systemMotionReduced = ref(false);
const reducedMotion = computed(() => requestedMotionReduction.value || systemMotionReduced.value);
const loading = ref(true);
const activity = ref<DonateActivity | null>(null);
const lifecycle = ref<DonateObsState>(createDonateObsState());
const error = ref("");
const prizeImageFailed = ref(false);
const aiCongratulation = ref("");
const videoReady = ref(false);
const videoFailed = ref(false);
const videoEnded = ref(false);
const videoTimedOut = ref(false);
const testPanelOpen = ref(false);
const testBusy = ref(false);
const testResult = ref("");
const testDonorName = ref("測試贊助者");
const testAmount = ref(100);
const testMessage = ref("這是一則 OBS Donate 測試通知");
const testPaymentMethod = ref("測試付款");
let refreshTimer: number | undefined;
let videoWatchdogTimer: number | undefined;
let motionQuery: MediaQueryList | undefined;
let routeGeneration = 0;
let refreshInProgress = false;
let refreshQueued = false;
let disposed = false;

const record = computed(() => lifecycle.value.current);
const phase = computed(() => lifecycle.value.phase);
const animationDuration = computed(() => Math.min(30, Math.max(3, activity.value?.animationDurationSeconds ?? 8)));
const animationKey = computed(() => ["ichiban", "gacha", "garagara", "money-box", "scratch-card"][activity.value?.animation ?? 0] ?? "ichiban");
const animationClass = computed(() => `animation-${animationKey.value}`);
const isGrandPrize = computed(() => !!record.value && activity.value?.prizes.some(prize =>
  prize.isGrandPrize && prize.name === record.value?.prizeName
) === true);
const ballClass = computed(() => `ball-${garagaraBallColor()}`);
const videoConfigured = computed(() => phase.value === "animation"
  && activity.value?.useWebmAnimation === true
  && !reducedMotion.value
  && !videoFailed.value
  && !!activity.value.webmAnimationUrl.trim());
const videoShouldHold = computed(() => shouldWaitForAnimationVideo({
  phase: phase.value,
  configured: videoConfigured.value,
  loop: activity.value?.webmAnimationLoop === true,
  reducedMotion: reducedMotion.value,
  ready: videoReady.value,
  failed: videoFailed.value,
  ended: videoEnded.value,
  timedOut: videoTimedOut.value
}));
const fallbackCongratulation = computed(() => {
  if (!record.value) return "";
  if (!record.value.isWinning) return `感謝 ${record.value.donorName} 的贊助與參與！`;
  return activity.value?.polaroidTemplateKey.toLocaleLowerCase() === "celebration"
    ? `${record.value.donorName} 的幸運時刻！${record.value.prizeName} 已經登場！`
    : `恭喜 ${record.value.donorName} 抽中 ${record.value.prizeName}！`;
});

function friendlyError(cause: unknown): string {
  if (cause instanceof ApiError && [401, 403].includes(cause.status)) {
    return "OBS 工作階段無效或權限不足，請從管理頁重新開啟預覽。";
  }
  return cause instanceof Error ? cause.message : "無法載入 Donate OBS 畫面。";
}

function clearVideoWatchdog(): void {
  window.clearTimeout(videoWatchdogTimer);
  videoWatchdogTimer = undefined;
}

function resetVideoState(): void {
  clearVideoWatchdog();
  videoReady.value = false;
  videoFailed.value = false;
  videoEnded.value = false;
  videoTimedOut.value = false;
}

function applyTransition(result: ReturnType<typeof advanceDonateReveal>): void {
  lifecycle.value = result.state;
  if (result.transition.isNewResult) {
    prizeImageFailed.value = false;
    aiCongratulation.value = "";
    resetVideoState();
  }
  if (result.transition.enteredReveal && record.value && activity.value?.useAiCongratulation && record.value.isWinning) {
    const activityAtRequest = activity.value;
    const recordId = record.value.id;
    void generateDonateCongratulation(activityAtRequest, record.value).then(message => {
      if (!disposed && lifecycle.value.current?.id === recordId && message) aiCongratulation.value = message;
    }).catch(() => {
      // The local template remains available when the optional AI endpoint fails.
    });
  }
  if (result.transition.cleared) {
    aiCongratulation.value = "";
    resetVideoState();
  }
}

function progressLifecycle(now = Date.now()): void {
  if (!activity.value) return;
  applyTransition(advanceDonateReveal(lifecycle.value, activity.value, now, !videoShouldHold.value));
}

async function refresh(): Promise<void> {
  if (refreshInProgress) {
    refreshQueued = true;
    return;
  }
  refreshInProgress = true;
  do {
    refreshQueued = false;
    const generation = routeGeneration;
    const requestedKey = routeKey.value;
    try {
      const snapshot = await readDonateSnapshot(requestedKey);
      if (disposed) break;
      if (generation !== routeGeneration) {
        refreshQueued = true;
        continue;
      }
      activity.value = snapshot.activity;
      if (!snapshot.activity) lifecycle.value = acceptDonateSnapshot(lifecycle.value, null, []);
      else {
        lifecycle.value = acceptDonateSnapshot(lifecycle.value, snapshot.activity.id, snapshot.records);
        progressLifecycle();
      }
      error.value = "";
    } catch (cause) {
      if (!disposed && generation === routeGeneration) error.value = friendlyError(cause);
    } finally {
      if (!disposed && generation === routeGeneration) loading.value = false;
    }
  } while (refreshQueued && !disposed);
  refreshInProgress = false;
}

watch(routeKey, () => {
  routeGeneration++;
  activity.value = null;
  lifecycle.value = createDonateObsState();
  error.value = "";
  loading.value = true;
  prizeImageFailed.value = false;
  aiCongratulation.value = "";
  resetVideoState();
  void refresh();
}, { immediate: true });

watch(reducedMotion, () => {
  if (reducedMotion.value) clearVideoWatchdog();
  progressLifecycle();
});

function onVideoReady(event: Event): void {
  if (!activity.value?.webmAnimationLoop && videoWatchdogTimer === undefined) {
    // A ready-but-stalled OBS stream must not block newer donations forever.
    videoWatchdogTimer = window.setTimeout(() => {
      videoWatchdogTimer = undefined;
      videoTimedOut.value = true;
      videoReady.value = false;
      progressLifecycle();
    }, animationDuration.value * 1_000 + 5_000);
  }
  const video = event.currentTarget as HTMLVideoElement;
  void video.play().then(() => {
    if (!disposed && !videoFailed.value && !videoTimedOut.value) videoReady.value = true;
  }).catch(onVideoFailed);
}

function onVideoFailed(): void {
  clearVideoWatchdog();
  videoFailed.value = true;
  videoReady.value = false;
  progressLifecycle();
}

function onVideoEnded(): void {
  clearVideoWatchdog();
  videoEnded.value = true;
  if (!activity.value) return;
  applyTransition(completeDonateAnimation(lifecycle.value, activity.value, Date.now()));
}

function garagaraBallColor(): string {
  if (!record.value?.isWinning || !activity.value) return "white";
  const prize = activity.value.prizes.find(item => item.name === record.value?.prizeName);
  if (prize?.isGrandPrize) return "gold";
  const candidates = activity.value.prizes.filter(item => item.remainingQuantity > 0 || item.quantity > 0);
  const white = [...candidates].sort((left, right) => left.probability - right.probability || right.remainingQuantity - left.remainingQuantity)[0];
  if (!white || prize?.id === white.id) return "white";
  const ordered = candidates.filter(item => item.id !== white.id).sort((left, right) => left.probability - right.probability);
  return ["blue", "green", "yellow", "red", "copper", "silver"][Math.min(5, Math.max(0, ordered.findIndex(item => item.id === prize?.id)))] ?? "white";
}

async function submitTestDonation(): Promise<void> {
  if (!activity.value || testBusy.value) return;
  testBusy.value = true;
  testResult.value = "";
  try {
    const result = await sendTestDonation(activity.value, {
      donorName: testDonorName.value,
      amount: testAmount.value,
      message: testMessage.value,
      paymentMethod: testPaymentMethod.value
    });
    testResult.value = !result.processed
      ? `測試失敗：${result.reason}`
      : result.records.length === 0
        ? `測試完成：贊助金額未達活動門檻 NT$${activity.value.minimumDonationAmount.toLocaleString("zh-TW")}。`
        : `測試通知已送出（${testPaymentMethod.value}）：${testDonorName.value} NT$${testAmount.value.toLocaleString("zh-TW")}，結果 ${result.records.map(item => item.prizeName).join("、")}。留言：${testMessage.value}`;
    await refresh();
  } catch (cause) {
    testResult.value = `測試失敗：${friendlyError(cause)}`;
  } finally {
    testBusy.value = false;
  }
}

onMounted(() => {
  motionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
  systemMotionReduced.value = motionQuery.matches;
  motionQuery.addEventListener("change", handleMotionPreference);
  refreshTimer = window.setInterval(() => { void refresh(); }, 2_000);
});

function handleMotionPreference(event: MediaQueryListEvent): void {
  systemMotionReduced.value = event.matches;
}

onBeforeUnmount(() => {
  disposed = true;
  routeGeneration++;
  window.clearInterval(refreshTimer);
  clearVideoWatchdog();
  motionQuery?.removeEventListener("change", handleMotionPreference);
});
</script>

<template>
  <main class="obs-layout donate-obs-layout" :class="{ 'obs-motion-reduced': reducedMotion }" aria-label="Donate OBS">
    <div class="donate-obs-shell">
      <section
        v-if="activity && record"
        :key="record.id"
        class="donate-reveal"
        :class="[animationClass, `phase-${phase}`, `polaroid-template-${activity.polaroidTemplateKey || 'classic'}`, { 'grand-prize': isGrandPrize }]"
        :style="{ '--draw-duration': `${animationDuration}s` }"
      >
        <section class="donate-notice" aria-label="新的 Donate 通知">
          <div class="donate-notice-eyebrow">NEW DONATE</div>
          <div class="donate-notice-name">{{ record.donorName }}</div>
          <div class="donate-notice-amount">NT${{ record.amount.toLocaleString("zh-TW") }}</div>
          <div v-if="record.donationMessage" class="donate-notice-message">{{ record.donationMessage }}</div>
          <div v-if="record.paymentMethod" class="donate-notice-payment">付款方式：{{ record.paymentMethod }}</div>
        </section>

        <div class="donate-reveal-label">{{ record.isWinning ? "DONATE RESULT" : "DONATE THANK YOU" }}</div>
        <div class="donate-animation-scene draw-prop" :class="[animationClass, { 'has-video': videoConfigured, 'video-ready': videoReady }]" aria-hidden="true">
          <video
            v-if="videoConfigured"
            :key="`${record.id}:${activity.webmAnimationUrl}`"
            class="donate-animation-video"
            :src="activity.webmAnimationUrl"
            :poster="activity.webmPosterUrl || undefined"
            autoplay
            muted
            playsinline
            preload="auto"
            :loop="activity.webmAnimationLoop"
            @canplay="onVideoReady"
            @error="onVideoFailed"
            @stalled="onVideoFailed"
            @ended="onVideoEnded"
          />
          <div class="donate-animation-css-fallback">
            <div v-if="animationKey === 'ichiban'" class="draw-scene ichiban-scene"><span>一番賞</span><b>特賞</b><i>✦</i></div>
            <div v-else-if="animationKey === 'gacha'" class="draw-scene gacha-scene"><span>✦</span><b>●</b><i>🎉</i></div>
            <div v-else-if="animationKey === 'garagara'" class="draw-scene garagara-scene"><span>◎</span><b :class="ballClass" /><i>✦</i></div>
            <div v-else-if="animationKey === 'money-box'" class="draw-scene moneybox-scene"><span>🐷</span><b>＄</b><i>＋</i></div>
            <div v-else class="draw-scene scratch-scene"><span>SCRATCH</span><b>{{ record.prizeName }}</b><i>✦</i></div>
          </div>
        </div>

        <div class="donate-result-content" role="status" aria-live="polite" aria-atomic="true">
          <template v-if="record.prizeImageUrl && !prizeImageFailed">
            <img class="donate-reveal-image" :src="record.prizeImageUrl" :alt="record.prizeName" @error="prizeImageFailed = true" />
            <div class="donate-reveal-prize">{{ record.prizeName }}</div>
          </template>
          <div v-else class="donate-reveal-fallback">{{ record.prizeName }}</div>
          <div class="donate-reveal-congratulation">{{ aiCongratulation || fallbackCongratulation }}</div>
          <div v-if="isGrandPrize" class="grand-prize-banner">★ 最大獎 ★</div>
        </div>
      </section>
      <div v-else-if="loading && controlsEnabled" class="donate-obs-state" role="status">載入 Donate OBS…</div>
      <div v-else-if="error && controlsEnabled" class="donate-obs-state" role="alert">{{ error }}</div>
    </div>

    <section v-if="controlsEnabled && testPanelOpen" class="donate-test-panel" aria-label="Donate 測試面板">
      <div class="donate-test-panel-header">
        <strong>Donate 測試模式</strong>
        <button class="donate-test-close" type="button" aria-label="關閉 Donate 測試面板" @click="testPanelOpen = false">×</button>
      </div>
      <p>會使用既有測試抽獎 endpoint 建立 OBS 通知，不會送出金流請求。</p>
      <form @submit.prevent="submitTestDonation">
        <label>測試贊助者名稱<input v-model="testDonorName" required /></label>
        <label>測試贊助金額<input v-model.number="testAmount" type="number" min="1" step="1" required /></label>
        <label>留言<input v-model="testMessage" /></label>
        <label>支付方式<input v-model="testPaymentMethod" /></label>
        <button type="submit" :disabled="testBusy">{{ testBusy ? "送出中…" : "送出測試 Donate" }}</button>
      </form>
      <div v-if="testResult" role="status">{{ testResult }}</div>
    </section>
    <button v-else-if="controlsEnabled" class="donate-test-launch" type="button" @click="testPanelOpen = true">測試 Donate</button>
  </main>
</template>
