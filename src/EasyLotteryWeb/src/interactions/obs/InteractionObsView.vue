<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { useRoute } from "vue-router";
import { readObsSessionToken } from "../../obs/session-token";
import {
  createInteractionRealtimeController,
  type InteractionConnectionStatus,
  type InteractionRoundSnapshot
} from "./realtime";
import "./interaction-obs.css";

type OverlayMode = "vote" | "notice" | "status" | "leaderboard";
const props = defineProps<{ mode: OverlayMode }>();
const route = useRoute();
const snapshot = ref<InteractionRoundSnapshot | null>(null);
const status = ref<InteractionConnectionStatus>("connecting");
const roundId = computed(() => String(route.params.roundId ?? ""));
const statusCopy: Record<InteractionConnectionStatus, string> = {
  connecting: "正在連接互動直播…",
  connected: "即時連線中",
  reconnecting: "即時連線中斷，正在重新連線…",
  disconnected: "即時連線中斷。請確認 OBS 網路後重試。",
  "retryable-error": "無法取得最新互動狀態，請重試即時連線。",
  "missing-token": "此 OBS 網址缺少讀取憑證，請從互動回合管理頁重新複製完整網址。",
  "invalid-token": "此 OBS 讀取憑證已失效，請從互動回合管理頁重新複製最新網址。",
  "not-found": "找不到這個互動回合，請確認網址中的回合識別碼。"
};
const statusLabel: Record<string, string> = {
  Draft: "草稿", Scheduled: "已排程", Live: "進行中", Paused: "已暫停", Settled: "已結算", Cancelled: "已取消"
};
const isProblem = computed(() => status.value === "disconnected" || status.value === "retryable-error" || status.value === "missing-token" || status.value === "invalid-token" || status.value === "not-found");
const options = computed(() => snapshot.value?.voteOptions ?? []);
const maxVotes = computed(() => Math.max(1, ...options.value.map(votesFor)));
const isEmptyVote = computed(() => options.value.length === 0 || options.value.every(option => votesFor(option) === 0));

let controller: ReturnType<typeof createInteractionRealtimeController> | null = null;

function votesFor(option: string): number {
  const key = Object.keys(snapshot.value?.voteCounts ?? {}).find(candidate => candidate.toLowerCase() === option.toLowerCase());
  return Math.max(0, key ? snapshot.value?.voteCounts[key] ?? 0 : 0);
}

function typeLabel(type: string): string {
  return ({ SignIn: "觀眾簽到", Vote: "即時投票", Quiz: "直播答題" } as Record<string, string>)[type] ?? type;
}

function reconnect(): void {
  if (status.value === "disconnected" || status.value === "retryable-error") void controller?.retry();
}

onMounted(() => {
  controller = createInteractionRealtimeController(roundId.value, readObsSessionToken(), {
    onSnapshot: value => { snapshot.value = value; },
    onStatus: value => { status.value = value; }
  });
  void controller.start();
});

onBeforeUnmount(() => {
  void controller?.dispose();
  controller = null;
});
</script>

<template>
  <main class="interaction-overlay" :class="[`interaction-overlay--${mode}`, { 'has-problem': isProblem }]" :aria-label="`互動 OBS ${mode}`">
    <div v-if="snapshot && !isProblem && (mode !== 'notice' || snapshot.status === 'Live')" class="interaction-panel" aria-live="polite" aria-atomic="true">
      <template v-if="mode === 'vote'">
        <header class="interaction-panel-header">
          <span class="interaction-eyebrow">LIVE · 觀眾投票</span>
          <h1>{{ snapshot.name }}</h1>
          <span class="interaction-live-indicator" :class="{ 'is-live': snapshot.status === 'Live' }"><span aria-hidden="true" />{{ statusLabel[snapshot.status] ?? snapshot.status }}</span>
        </header>
        <p v-if="isEmptyVote" class="interaction-empty">投票開始後，結果會即時顯示在這裡。</p>
        <div v-else class="interaction-vote-list">
          <div v-for="(option, index) in options" :key="option" class="interaction-vote-option">
            <div class="interaction-vote-label"><span>{{ option }}</span><strong>{{ votesFor(option) }}</strong></div>
            <div class="interaction-vote-track" role="progressbar" :aria-label="`${option} 得票數`" :aria-valuemin="0" :aria-valuemax="maxVotes" :aria-valuenow="votesFor(option)">
              <span :style="{ width: `${Math.round(votesFor(option) / maxVotes * 100)}%`, '--option-index': index }" />
            </div>
          </div>
        </div>
      </template>

      <template v-else-if="mode === 'notice'">
        <div class="interaction-live-notice">
          <span class="interaction-notice-mark" aria-hidden="true">✦</span>
          <div><span class="interaction-eyebrow">現在開放互動</span><h1>{{ snapshot.name }}</h1><p>在聊天室參與 {{ typeLabel(snapshot.type) }}</p></div>
        </div>
      </template>

      <template v-else-if="mode === 'status'">
        <div class="interaction-status-card">
          <span class="interaction-live-indicator" :class="{ 'is-live': snapshot.status === 'Live' }"><span aria-hidden="true" />{{ statusLabel[snapshot.status] ?? snapshot.status }}</span>
          <h1>{{ snapshot.name }}</h1>
          <p>{{ typeLabel(snapshot.type) }}<span aria-hidden="true"> · </span>{{ snapshot.status }}</p>
        </div>
      </template>

      <template v-else>
        <header class="interaction-panel-header">
          <span class="interaction-eyebrow">本回合積分榜</span>
          <h1>{{ snapshot.name }}</h1>
        </header>
        <p v-if="snapshot.leaderboard.length === 0" class="interaction-empty">目前還沒有積分，觀眾參與後會出現在這裡。</p>
        <TransitionGroup v-else tag="ol" name="leaderboard" class="interaction-leaderboard">
          <li v-for="(entry, index) in snapshot.leaderboard" :key="entry.audienceProfileId" :class="{ 'is-first': index === 0 }">
            <span class="interaction-rank">{{ String(index + 1).padStart(2, '0') }}</span>
            <span class="interaction-player">{{ entry.displayName }}</span>
            <strong class="interaction-score">{{ entry.points }}<small> pts</small></strong>
          </li>
        </TransitionGroup>
      </template>

      <footer class="interaction-panel-footer"><span class="interaction-connection-dot" :class="`is-${status}`" aria-hidden="true" />{{ status === "reconnecting" ? "重新連線中" : "即時同步" }}</footer>
    </div>

    <div v-else-if="status !== 'connected' && status !== 'connecting' && status !== 'reconnecting'" class="interaction-connection-alert" role="alert">
      <span class="interaction-alert-icon" aria-hidden="true">!</span>
      <p>{{ statusCopy[status] }}</p>
      <button v-if="status === 'disconnected' || status === 'retryable-error'" type="button" @click="reconnect">重新連線</button>
    </div>
    <p v-else-if="!snapshot && status !== 'connected'" class="interaction-connection-hint" role="status">{{ statusCopy[status] }}</p>
  </main>
</template>
