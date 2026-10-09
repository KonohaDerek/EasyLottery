<script setup lang="ts">
import { onMounted, ref } from "vue";
import { type PaymentSettings, emptyPaymentSettings, loadPaymentSettings, savePaymentSettings } from "../api/settings";

const settings = ref<PaymentSettings>(emptyPaymentSettings());
const etag = ref("");
const loading = ref(true);
const saving = ref(false);
const error = ref("");
const saved = ref(false);

async function load() {
  try {
    const response = await loadPaymentSettings();
    settings.value = response.data;
    etag.value = response.etag;
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "YouTube 設定載入失敗。";
  } finally {
    loading.value = false;
  }
}

async function save() {
  saving.value = true;
  error.value = "";
  saved.value = false;
  try {
    const response = await savePaymentSettings(settings.value, etag.value);
    settings.value = response.data;
    etag.value = response.etag || etag.value;
    saved.value = true;
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "YouTube 設定儲存失敗。";
  } finally {
    saving.value = false;
  }
}

onMounted(load);
</script>

<template>
  <section class="settings-page" aria-labelledby="youtube-title">
    <div class="page-heading"><p class="eyebrow">YOUTUBE</p><h1 id="youtube-title">YouTube API Key</h1><p class="lead">此頁只更新 YouTube 欄位，其他支付與通知設定會完整保留。</p></div>
    <v-card class="settings-card">
      <v-card-text>
        <p v-if="loading" class="status-text">載入中…</p>
        <form v-else @submit.prevent="save">
          <label class="field-label" for="youtube-api-key">API Key</label>
          <input id="youtube-api-key" v-model="settings.youTube.apiKey" class="settings-input" type="password" autocomplete="new-password" />
          <label class="checkbox-label"><input v-model="settings.enableYouTubeSuperChat" type="checkbox" /> 啟用 YouTube Super Chat</label>
          <div class="settings-actions"><button class="submit-button compact" type="submit" :disabled="saving">{{ saving ? "儲存中…" : "儲存 YouTube 設定" }}</button><span v-if="saved" class="success-text" role="status">已儲存</span></div>
        </form>
        <div v-if="error" class="login-error" role="alert">{{ error }}</div>
      </v-card-text>
    </v-card>
  </section>
</template>
