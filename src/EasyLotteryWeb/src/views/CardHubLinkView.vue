<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { ApiError } from "../api/client";
import { loadHubLinkSettings, saveHubLinkSettings, testHubLink, type HubLinkSettings, type HubLinkStatus } from "../shop/api";

const settings = ref<HubLinkSettings>({
  hubBaseUrl: "",
  siteClientId: "",
  hasClientSecret: false,
  status: "not_linked",
  tenantId: null,
  lastPublishAt: null
});
const clientSecret = ref("");
const loading = ref(true);
const loaded = ref(false);
const saving = ref(false);
const testing = ref(false);
const error = ref("");
const saved = ref(false);

const statusPresentation: Record<HubLinkStatus, { label: string; color: string }> = {
  not_linked: { label: "尚未連結", color: "default" },
  linked: { label: "已連結", color: "success" },
  credentials_invalid: { label: "憑證無效", color: "error" },
  hub_unreachable: { label: "Hub 無法連線", color: "warning" }
};
const status = computed(() => statusPresentation[settings.value.status]);

function requestBody() {
  return {
    hubBaseUrl: settings.value.hubBaseUrl,
    siteClientId: settings.value.siteClientId,
    clientSecret: clientSecret.value
  };
}

function applyResult(result: HubLinkSettings) {
  settings.value = result;
  saved.value = false;
}

async function load() {
  loading.value = true;
  loaded.value = false;
  error.value = "";
  try {
    applyResult(await loadHubLinkSettings());
    loaded.value = true;
  } catch (cause) {
    error.value = cause instanceof ApiError ? cause.message : "Card Hub 設定載入失敗，請稍後重試。";
  } finally {
    loading.value = false;
  }
}

async function testConnection() {
  testing.value = true;
  error.value = "";
  saved.value = false;
  try {
    applyResult(await testHubLink(requestBody()));
  } catch (cause) {
    error.value = cause instanceof ApiError ? cause.message : "測試連線失敗，請確認 Hub 網址後重試。";
  } finally {
    testing.value = false;
  }
}

async function save() {
  saving.value = true;
  error.value = "";
  saved.value = false;
  try {
    applyResult(await saveHubLinkSettings(requestBody()));
    clientSecret.value = "";
    saved.value = true;
  } catch (cause) {
    error.value = cause instanceof ApiError ? cause.message : "Card Hub 設定儲存失敗，請稍後重試。";
  } finally {
    saving.value = false;
  }
}

onMounted(load);
</script>

<template>
  <section class="settings-page hub-link-page" aria-labelledby="hub-link-title">
    <header class="page-heading">
      <p class="eyebrow">CARD HUB</p>
      <h1 id="hub-link-title">Card Hub 連結</h1>
      <p class="lead">連結 Card Hub 後，網站才能將卡牌目錄發布至你的租戶。</p>
    </header>

    <p v-if="loading" class="status-text" role="status">載入 Card Hub 設定與連線狀態…</p>
    <div v-else-if="!loaded" class="login-error" role="alert">
      <p>{{ error || "Card Hub 設定尚未載入。" }}</p>
      <button class="submit-button compact" type="button" @click="load">重新載入</button>
    </div>
    <form v-else class="settings-form" @submit.prevent="save">
      <v-card class="settings-card">
        <v-card-title class="hub-link-status-title">
          連線狀態
          <v-chip :color="status.color" variant="tonal" size="small" role="status">{{ status.label }}</v-chip>
        </v-card-title>
        <v-card-text class="form-grid">
          <div>
            <label class="field-label" for="hub-base-url">Hub base URL</label>
            <input id="hub-base-url" v-model.trim="settings.hubBaseUrl" class="settings-input" type="url" placeholder="https://card-hub.example.com" autocomplete="url" required />
          </div>
          <div>
            <label class="field-label" for="hub-site-client-id">Site client id</label>
            <input id="hub-site-client-id" v-model.trim="settings.siteClientId" class="settings-input" type="text" autocomplete="off" required maxlength="200" />
          </div>
          <div class="hub-secret-field">
            <label class="field-label" for="hub-client-secret">Client secret</label>
            <input id="hub-client-secret" v-model="clientSecret" class="settings-input" type="password" autocomplete="new-password" placeholder="輸入 Card Hub 提供的密鑰" />
            <p v-if="settings.hasClientSecret" class="field-hint">已儲存密鑰；留白會沿用現有密鑰。密鑰不會由伺服器回傳。</p>
            <p v-else class="field-hint">密鑰只會送到伺服器加密保存，儲存後欄位會清空，不會再次顯示。</p>
          </div>
        </v-card-text>
      </v-card>

      <p v-if="error" class="login-error" role="alert">{{ error }}</p>
      <div class="settings-actions">
        <button class="submit-button compact" type="button" :disabled="testing || saving" @click="testConnection">{{ testing ? "測試中…" : "測試連線" }}</button>
        <button class="submit-button compact" type="submit" :disabled="saving || testing">{{ saving ? "儲存中…" : "儲存設定" }}</button>
        <span v-if="saved" class="success-text" role="status">已儲存，{{ status.label }}</span>
      </div>
    </form>
  </section>
</template>

<style scoped>
.hub-link-page { max-width: 960px; margin-inline: auto; }
.hub-link-status-title { display: flex; align-items: center; justify-content: space-between; gap: 1rem; }
.hub-secret-field { grid-column: 1 / -1; }
.field-hint { margin: .4rem 0 0; color: var(--creator-muted); font-size: .9rem; }
</style>
