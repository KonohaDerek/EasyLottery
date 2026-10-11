<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { ApiError } from "../../api/client";
import {
  createInteractionRound,
  listInteractionRounds,
  transitionInteractionRound,
  type InteractionRound
} from "./api";

const rounds = ref<InteractionRound[] | null>(null);
const loading = ref(true);
const saving = ref(false);
const activeAction = ref<string | null>(null);
const error = ref("");
const formError = ref("");
const name = ref("");
const type = ref<"SignIn" | "Vote" | "Quiz">("Vote");
const commandPrefix = ref("!vote");
const voteOptions = ref("A,B");
const correctAnswer = ref("");
const correctAnswerPoints = ref(1);
const eligibilityTickets = ref(0);

const canSubmit = computed(() => name.value.trim().length > 0 && commandPrefix.value.trim().length > 0 && !saving.value);

function errorMessage(cause: unknown, fallback: string): string {
  if (cause instanceof ApiError) {
    if (cause.status === 401 || cause.status === 403) return "管理員工作階段已失效，請重新登入後再試。";
    if (cause.status === 429) return "操作太頻繁，請稍後再試。";
    return cause.message || fallback;
  }
  return fallback;
}

async function reload(): Promise<void> {
  loading.value = true;
  error.value = "";
  try {
    rounds.value = await listInteractionRounds();
  } catch (cause) {
    error.value = errorMessage(cause, "目前無法載入互動回合，請稍後重試。");
  } finally {
    loading.value = false;
  }
}

async function createRound(): Promise<void> {
  formError.value = "";
  if (!canSubmit.value) return;
  if (type.value === "Vote" && voteOptions.value.split(",").map(value => value.trim()).filter(Boolean).length < 2) {
    formError.value = "投票回合至少需要兩個選項，請以逗號分隔。";
    return;
  }

  saving.value = true;
  try {
    await createInteractionRound({
      name: name.value.trim(),
      type: type.value,
      commandPrefix: commandPrefix.value.trim(),
      voteOptions: type.value === "Vote" ? voteOptions.value.split(",").map(value => value.trim()).filter(Boolean) : [],
      correctAnswer: type.value === "Quiz" ? correctAnswer.value.trim() || null : null,
      correctAnswerPoints: type.value === "Quiz" ? Math.max(0, Number(correctAnswerPoints.value) || 0) : 0,
      eligibilityTickets: Math.max(0, Number(eligibilityTickets.value) || 0)
    });
    name.value = "";
    correctAnswer.value = "";
    await reload();
  } catch (cause) {
    formError.value = errorMessage(cause, "無法建立回合，請確認資料後重試。");
  } finally {
    saving.value = false;
  }
}

async function transition(round: InteractionRound, action: "start" | "pause" | "settle" | "cancel"): Promise<void> {
  const key = `${round.id}:${action}`;
  activeAction.value = key;
  error.value = "";
  try {
    await transitionInteractionRound(round.id, action);
    await reload();
  } catch (cause) {
    error.value = errorMessage(cause, "回合狀態更新失敗；狀態未確認前請勿重複操作。");
  } finally {
    activeAction.value = null;
  }
}

function statusLabel(status: string): string {
  return ({ Draft: "草稿", Scheduled: "已排程", Live: "進行中", Paused: "已暫停", Settled: "已結算", Cancelled: "已取消" } as Record<string, string>)[status] ?? status;
}

onMounted(() => void reload());
</script>

<template>
  <section class="settings-page interaction-admin-page" aria-labelledby="interaction-rounds-title">
    <header class="page-heading">
      <h1 id="interaction-rounds-title">直播互動回合</h1>
      <p class="lead">建立簽到、投票或答題回合，並從這裡控制直播中的互動狀態。</p>
    </header>

    <v-alert v-if="error" type="error" variant="tonal" role="alert" class="interaction-alert">
      <div class="d-flex align-center justify-space-between ga-3 flex-wrap">
        <span>{{ error }}</span>
        <v-btn variant="outlined" size="small" :disabled="loading" @click="reload">重新載入</v-btn>
      </div>
    </v-alert>

    <v-card class="interaction-form-card" rounded="xl" variant="outlined">
      <v-card-item>
        <v-card-title>建立互動回合</v-card-title>
        <v-card-subtitle>新回合會先以草稿保存，不會直接對觀眾開放。</v-card-subtitle>
      </v-card-item>
      <v-card-text>
        <form class="interaction-form" @submit.prevent="createRound">
          <v-text-field v-model="name" label="回合名稱" maxlength="80" required autocomplete="off" />
          <div class="interaction-form-grid">
            <v-select v-model="type" label="互動類型" :items="[
              { title: '投票', value: 'Vote' },
              { title: '簽到', value: 'SignIn' },
              { title: '答題', value: 'Quiz' }
            ]" />
            <v-text-field v-model="commandPrefix" label="聊天室指令" maxlength="24" required autocomplete="off" />
          </div>
          <v-text-field v-if="type === 'Vote'" v-model="voteOptions" label="投票選項（以逗號分隔）" hint="例如：A,B,C" persistent-hint autocomplete="off" />
          <template v-if="type === 'Quiz'">
            <v-text-field v-model="correctAnswer" label="正確答案（可稍後再設定）" autocomplete="off" />
            <v-text-field v-model.number="correctAnswerPoints" label="答對得分" type="number" min="0" />
          </template>
          <v-text-field v-model.number="eligibilityTickets" label="抽選資格票數" type="number" min="0" hint="0 表示不建立抽選資格快照。" persistent-hint />
          <v-alert v-if="formError" type="error" variant="tonal" role="alert">{{ formError }}</v-alert>
          <div class="interaction-form-actions">
            <v-btn color="primary" type="submit" :loading="saving" :disabled="!canSubmit">建立草稿</v-btn>
          </div>
        </form>
      </v-card-text>
    </v-card>

    <div class="interaction-list-heading">
      <h2>已建立的回合</h2>
      <v-btn variant="text" :disabled="loading" @click="reload">重新整理</v-btn>
    </div>
    <div v-if="loading" class="interaction-loading" role="status">正在載入互動回合…</div>
    <v-alert v-else-if="rounds?.length === 0" type="info" variant="tonal">目前沒有互動回合；建立草稿後會顯示在這裡。</v-alert>
    <div v-else class="interaction-round-list">
      <v-card v-for="round in rounds" :key="round.id" class="interaction-round-card" rounded="xl" variant="outlined" :data-round-id="round.id">
        <v-card-item>
          <template #append>
            <v-chip :color="round.status === 'Live' ? 'success' : round.status === 'Paused' ? 'warning' : 'default'" size="small">
              {{ statusLabel(round.status) }}
            </v-chip>
          </template>
          <v-card-title>{{ round.name }}</v-card-title>
          <v-card-subtitle>{{ round.type }} · 指令 {{ round.commandPrefix }}</v-card-subtitle>
        </v-card-item>
        <v-card-text v-if="round.voteOptions.length" class="interaction-options">選項：{{ round.voteOptions.join("、") }}</v-card-text>
        <v-card-actions class="interaction-round-actions">
          <v-btn v-if="round.status === 'Draft' || round.status === 'Scheduled'" color="success" variant="tonal" :loading="activeAction === `${round.id}:start`" :disabled="activeAction !== null" @click="transition(round, 'start')">開始</v-btn>
          <v-btn v-if="round.status === 'Live'" color="warning" variant="tonal" :loading="activeAction === `${round.id}:pause`" :disabled="activeAction !== null" @click="transition(round, 'pause')">暫停</v-btn>
          <v-btn v-if="round.status === 'Live' || round.status === 'Paused'" color="primary" variant="tonal" :loading="activeAction === `${round.id}:settle`" :disabled="activeAction !== null" @click="transition(round, 'settle')">結算</v-btn>
          <v-btn v-if="round.status === 'Draft' || round.status === 'Scheduled' || round.status === 'Live' || round.status === 'Paused'" color="error" variant="text" :loading="activeAction === `${round.id}:cancel`" :disabled="activeAction !== null" @click="transition(round, 'cancel')">取消回合</v-btn>
        </v-card-actions>
      </v-card>
    </div>
  </section>
</template>

<style scoped>
.interaction-admin-page { --interaction-accent: #6d4aff; }
.interaction-form-card { border-color: rgba(92, 76, 180, 0.18); }
.interaction-form { display: grid; gap: 0.8rem; }
.interaction-form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; }
.interaction-form-actions, .interaction-list-heading, .interaction-round-actions { display: flex; align-items: center; gap: 0.5rem; }
.interaction-list-heading { justify-content: space-between; margin-top: 0.5rem; }
.interaction-list-heading h2 { margin: 0; font-size: 1.25rem; }
.interaction-round-list { display: grid; gap: 0.85rem; }
.interaction-round-card { border-color: rgba(92, 76, 180, 0.16); transition: border-color 180ms ease, box-shadow 180ms ease; }
.interaction-round-card:hover { border-color: rgba(92, 76, 180, 0.4); box-shadow: 0 10px 28px rgba(47, 38, 101, 0.08); }
.interaction-options { padding-top: 0; color: #64748b; }
.interaction-loading { padding: 1rem 0; color: #64748b; }
@media (max-width: 680px) { .interaction-form-grid { grid-template-columns: 1fr; gap: 0; } }
@media (prefers-reduced-motion: reduce) { .interaction-round-card { transition: none; } }
</style>
