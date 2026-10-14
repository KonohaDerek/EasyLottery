<script setup lang="ts">
import { onMounted, ref } from "vue";
import { ApiError } from "../api/client";
import {
  loadPlatformConnections,
  savePlatformConnection,
  toPlatformDraft,
  type PlatformConnectionDraft
} from "../platform-connections/api";

const drafts = ref<PlatformConnectionDraft[]>([]);
const etag = ref("");
const loading = ref(true);
const saving = ref("");
const error = ref("");
const success = ref("");
const conflict = ref(false);

async function reload(): Promise<void> {
  loading.value = true;
  error.value = "";
  success.value = "";
  conflict.value = false;
  try {
    const snapshot = await loadPlatformConnections();
    etag.value = snapshot.etag;
    drafts.value = snapshot.data.map(toPlatformDraft);
  } catch (cause) {
    drafts.value = [];
    etag.value = "";
    error.value = cause instanceof ApiError ? cause.message : "無法載入平台串接設定。";
  } finally {
    loading.value = false;
  }
}

async function save(draft: PlatformConnectionDraft): Promise<void> {
  if (!etag.value || conflict.value || saving.value) return;
  saving.value = draft.platform;
  error.value = "";
  success.value = "";
  try {
    const snapshot = await savePlatformConnection(draft, etag.value);
    etag.value = snapshot.etag;
    draft.state = snapshot.data.state;
    draft.configured = snapshot.data.configured;
    draft.credentialValue = "";
    draft.credentialAction = "unchanged";
    success.value = `${draft.platform} 設定已儲存。`;
  } catch (cause) {
    if (cause instanceof ApiError && cause.status === 409) {
      conflict.value = true;
      error.value = "設定已由其他管理員更新。請重新載入最新內容後再儲存。";
    } else {
      error.value = cause instanceof ApiError ? cause.message : `${draft.platform} 設定儲存失敗。`;
    }
  } finally {
    saving.value = "";
  }
}

function stateLabel(state: string): string {
  return state === "configured" ? "已連線" : state === "degraded" ? "連線異常" : "尚未設定";
}

function stateColor(state: string): string {
  return state === "configured" ? "success" : state === "degraded" ? "warning" : "default";
}

function credentialLabel(platform: string): string {
  return platform === "YouTube" ? "互動平台 YouTube API Key" : platform === "Twitch" ? "Twitch access token" : "Discord Bot token";
}

function credentialChanged(draft: PlatformConnectionDraft, value: string | null): void {
  draft.credentialValue = value ?? "";
  draft.credentialAction = draft.credentialValue ? "replace" : "unchanged";
}

onMounted(() => void reload());
</script>

<template>
  <main class="settings-page" aria-labelledby="connections-title">
    <header class="page-heading">
      <p class="eyebrow">LIVE PLATFORMS</p>
      <h1 id="connections-title">直播平台串接</h1>
      <p class="lead">憑證只由伺服器保存。留白代表保留原憑證；如需移除，請使用明確的清除操作。</p>
    </header>

    <v-alert v-if="error" type="error" variant="tonal" role="alert" class="mb-4">
      {{ error }}
      <template #append><v-btn variant="text" @click="reload">{{ conflict ? "重新載入最新設定" : "重新載入" }}</v-btn></template>
    </v-alert>
    <p v-if="success" class="success-text mb-4" role="status">{{ success }}</p>
    <div v-if="loading" class="state-card" role="status">載入平台串接設定…</div>
    <div v-else-if="!error && drafts.length === 0" class="state-card" role="status">伺服器目前沒有可管理的平台。</div>

    <v-row v-else-if="drafts.length">
      <v-col v-for="draft in drafts" :key="draft.platform" cols="12" lg="6">
        <v-card class="connection-card" rounded="xl" elevation="1">
          <v-card-item>
            <v-card-title>{{ draft.platform }}</v-card-title>
            <template #append><v-chip :color="stateColor(draft.state)" size="small" variant="tonal">{{ stateLabel(draft.state) }}</v-chip></template>
          </v-card-item>
          <v-card-text>
            <v-text-field v-model="draft.channelScope" label="頻道範圍" variant="outlined" density="comfortable" autocomplete="off" />
            <v-text-field
              :model-value="draft.credentialValue"
              :label="credentialLabel(draft.platform)"
              :placeholder="draft.configured ? '已設定；留白以保留目前憑證' : '尚未設定憑證'"
              type="password"
              autocomplete="new-password"
              variant="outlined"
              density="comfortable"
              @update:model-value="credentialChanged(draft, $event)"
            />
            <p v-if="draft.platform === 'YouTube'" class="field-hint">
              這是獨立的互動平台設定；抽獎工作區的 YouTube Live Chat 不讀取此欄位。請到
              <RouterLink to="/system/youtube-login">YouTube 直播設定</RouterLink>
              管理 Live Chat 使用的系統 API Key。
            </p>
            <p v-if="draft.platform === 'Discord'" class="field-hint">頻道範圍請填 GuildId:ChannelId。</p>
            <div class="connection-actions">
              <v-btn
                v-if="draft.credentialAction !== 'unchanged'"
                variant="text"
                @click="draft.credentialValue = ''; draft.credentialAction = 'unchanged'"
              >保留目前憑證</v-btn>
              <v-btn
                v-if="draft.configured"
                variant="text"
                color="error"
                :aria-label="`${draft.platform} ${draft.credentialAction === 'clear' ? '已選擇清除憑證' : '清除憑證'}`"
                :disabled="conflict || !!saving"
                @click="draft.credentialValue = ''; draft.credentialAction = 'clear'"
              >{{ draft.credentialAction === 'clear' ? '已選擇清除' : '清除憑證' }}</v-btn>
              <v-spacer />
              <v-btn color="primary" :loading="saving === draft.platform" :disabled="loading || !!saving || conflict || !etag" @click="save(draft)">
                儲存 {{ draft.platform }}
              </v-btn>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </main>
</template>

<style scoped>
.connection-card { height: 100%; border: 1px solid var(--creator-border); }
.connection-actions { display: flex; align-items: center; flex-wrap: wrap; gap: 0.35rem; }
.field-hint { margin: -0.5rem 0 1rem; color: var(--creator-muted); font-size: 0.875rem; }
.state-card { padding: 1.25rem; color: var(--creator-muted); background: var(--creator-surface); border: 1px solid var(--creator-border); border-radius: 1rem; }
.success-text { color: #147a43; font-weight: 650; }
</style>
