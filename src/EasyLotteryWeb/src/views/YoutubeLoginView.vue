<script setup lang="ts">
import { onMounted, ref } from "vue";
import { type PaymentSettings, emptyPaymentSettings, loadPaymentSettings, savePaymentSettings } from "../api/settings";
import { getYouTubeCredentialStatus } from "../lottery/api";

const settings = ref<PaymentSettings>(emptyPaymentSettings());
const etag = ref("");
const loading = ref(true);
const loaded = ref(false);
const saving = ref(false);
const error = ref("");
const saved = ref(false);
const credentialStatus = ref<"checking" | "configured" | "missing" | "unavailable">("checking");

async function refreshCredentialStatus() {
  try {
    credentialStatus.value = (await getYouTubeCredentialStatus()).configured ? "configured" : "missing";
  } catch {
    credentialStatus.value = "unavailable";
  }
}

async function load() {
  loading.value = true;
  loaded.value = false;
  error.value = "";
  try {
    const response = await loadPaymentSettings();
    if (!response.etag) throw new Error("設定載入缺少 ETag，請重新載入後再儲存。");
    settings.value = response.data;
    etag.value = response.etag;
    loaded.value = true;
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "YouTube 設定載入失敗。";
  } finally {
    loading.value = false;
  }
}

async function save() {
  if (!loaded.value || !etag.value) {
    error.value = "設定尚未成功載入，請重新載入後再儲存。";
    return;
  }

  saving.value = true;
  error.value = "";
  saved.value = false;
  try {
    const response = await savePaymentSettings(settings.value, etag.value);
    settings.value = response.data;
    etag.value = response.etag || etag.value;
    saved.value = true;
    await refreshCredentialStatus();
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "YouTube 設定儲存失敗。";
  } finally {
    saving.value = false;
  }
}

onMounted(() => {
  void load();
  void refreshCredentialStatus();
});
</script>

<template>
  <section class="settings-page" aria-labelledby="youtube-title">
    <div class="page-heading"><p class="eyebrow">YOUTUBE LIVE</p><h1 id="youtube-title">YouTube 直播設定</h1><p class="lead">抽獎工作區的 YouTube Live Chat 與 Super Chat 共用此處設定；API Key 只由伺服器保存與使用。</p></div>
    <v-card class="settings-card">
      <v-card-text>
        <p class="youtube-status" role="status" aria-live="polite">
          {{ credentialStatus === "checking" ? "正在確認伺服器設定…" : credentialStatus === "configured" ? "伺服器已設定 API Key（此狀態不代表 Google API 已完成實測）。" : credentialStatus === "missing" ? "尚未設定可用的 YouTube API Key。" : "目前無法確認 API Key 狀態，請稍後重試。" }}
        </p>
        <p class="field-hint">請先在 Google Cloud 啟用 YouTube Data API v3，再於此頁貼上 API Key。儲存後可回抽獎工作區使用正在直播的 YouTube 網址開始擷取；無效金鑰或 API 配額錯誤會在操作時顯示。若金鑰由伺服器環境設定提供，請由部署者更新。</p>
        <p v-if="loading" class="status-text">載入中…</p>
        <div v-else-if="!loaded" class="login-error" role="alert">
          <p>{{ error || "YouTube 設定尚未載入。" }}</p>
          <button class="submit-button compact" type="button" @click="load">重新載入</button>
        </div>
        <form v-else @submit.prevent="save">
          <label class="field-label" for="youtube-api-key">YouTube Data API Key</label>
          <input id="youtube-api-key" v-model="settings.youTube.apiKey" class="settings-input" type="password" autocomplete="new-password" placeholder="已設定時可留白保留伺服器值" />
          <label class="checkbox-label"><input v-model="settings.enableYouTubeSuperChat" type="checkbox" /> 啟用 YouTube Super Chat</label>
          <div class="settings-actions"><button class="submit-button compact" type="submit" :disabled="saving">{{ saving ? "儲存中…" : "儲存 YouTube 設定" }}</button><span v-if="saved" class="success-text" role="status">已儲存</span></div>
        </form>
        <div v-if="error && loaded" class="login-error" role="alert">{{ error }}</div>
      </v-card-text>
    </v-card>
  </section>
</template>

<style scoped>
.youtube-status { margin: 0 0 0.5rem; color: var(--creator-ink); font-weight: 750; }
.field-hint { margin: 0 0 1rem; color: var(--creator-muted); }
</style>
