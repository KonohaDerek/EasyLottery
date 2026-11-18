<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { ApiError } from "../api/client";
import {
  formatHubLinkTimestamp,
  getHubLinkStatusCopy,
  getHubLinkTestErrorFeedback,
  getHubLinkTestFeedback,
  hubLinkCopy
} from "../shop/copy";
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
const lastCheckAt = ref<string | null>(null);
const testFeedback = ref<ReturnType<typeof getHubLinkTestFeedback>>(null);
let testFeedbackTimeout: ReturnType<typeof setTimeout> | undefined;

function showTestFeedback(feedback: ReturnType<typeof getHubLinkTestFeedback>) {
  if (testFeedbackTimeout) clearTimeout(testFeedbackTimeout);
  testFeedback.value = feedback;
  if (feedback) {
    testFeedbackTimeout = setTimeout(() => {
      testFeedback.value = null;
      testFeedbackTimeout = undefined;
    }, 5000);
  }
}

onBeforeUnmount(() => {
  if (testFeedbackTimeout) clearTimeout(testFeedbackTimeout);
});

const statusColors: Record<HubLinkStatus, string> = {
  not_linked: "default",
  linked: "success",
  credentials_invalid: "error",
  hub_unreachable: "warning"
};
const status = computed(() => ({
  ...getHubLinkStatusCopy(settings.value.status),
  color: statusColors[settings.value.status]
}));

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
  showTestFeedback(null);
  try {
    const result = await loadHubLinkSettings();
    applyResult(result);
    loaded.value = true;
    if (result.hasClientSecret) lastCheckAt.value = new Date().toISOString();
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
  showTestFeedback(null);
  try {
    const result = await testHubLink(requestBody());
    if (result.status === "tenant_disabled") {
      applyResult({ ...result, status: "linked" });
    } else {
      applyResult(result);
    }
    lastCheckAt.value = new Date().toISOString();
    showTestFeedback(getHubLinkTestFeedback(result.status));
  } catch (cause) {
    showTestFeedback(getHubLinkTestErrorFeedback(cause instanceof ApiError ? cause.code : ""));
  } finally {
    testing.value = false;
  }
}

async function save() {
  saving.value = true;
  error.value = "";
  saved.value = false;
  showTestFeedback(null);
  try {
    applyResult(await saveHubLinkSettings(requestBody()));
    clientSecret.value = "";
    saved.value = true;
    lastCheckAt.value = new Date().toISOString();
  } catch {
    error.value = hubLinkCopy["hub.link.toast.saveFailed"];
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
      <h1 id="hub-link-title">{{ hubLinkCopy["hub.link.title"] }}</h1>
      <p class="lead">{{ hubLinkCopy["hub.link.lead"] }}</p>
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
          <p class="hub-link-status-copy">{{ status.desc }}</p>
          <p class="hub-link-status-action">{{ status.action }}</p>

          <div>
            <label class="field-label" for="hub-base-url">{{ hubLinkCopy["hub.link.field.hubUrl.label"] }}</label>
            <input id="hub-base-url" v-model.trim="settings.hubBaseUrl" class="settings-input" type="url" placeholder="https://card-hub.example.com" autocomplete="url" required />
            <p class="field-hint">{{ hubLinkCopy["hub.link.field.hubUrl.helper"] }}</p>
          </div>
          <div>
            <label class="field-label" for="hub-site-client-id">{{ hubLinkCopy["hub.link.field.clientId.label"] }}</label>
            <input id="hub-site-client-id" v-model.trim="settings.siteClientId" class="settings-input" type="text" autocomplete="off" required maxlength="200" />
            <p class="field-hint">{{ hubLinkCopy["hub.link.field.clientId.helper"] }}</p>
          </div>
          <div class="hub-secret-field">
            <label class="field-label" for="hub-client-secret">{{ hubLinkCopy["hub.link.field.clientSecret.label"] }}</label>
            <input id="hub-client-secret" v-model="clientSecret" class="settings-input" type="password" autocomplete="new-password" :placeholder="settings.hasClientSecret ? hubLinkCopy['hub.link.field.clientSecret.placeholderSaved'] : undefined" />
            <p class="field-hint">{{ hubLinkCopy["hub.link.field.clientSecret.helper"] }}</p>
            <p class="field-hint rotation-hint">{{ hubLinkCopy["hub.site.rotate.explain"] }}</p>
          </div>

          <dl class="hub-link-metadata">
            <div>
              <dt>{{ hubLinkCopy["hub.link.field.tenant.label"] }}</dt>
              <dd>{{ settings.tenantId || "—" }}</dd>
            </div>
            <div>
              <dt>{{ hubLinkCopy["hub.link.field.lastPublish.label"] }}</dt>
              <dd>{{ formatHubLinkTimestamp(settings.lastPublishAt) }}</dd>
            </div>
            <div>
              <dt>{{ hubLinkCopy["hub.link.field.lastCheck.label"] }}</dt>
              <dd>{{ formatHubLinkTimestamp(lastCheckAt) }}</dd>
            </div>
          </dl>
        </v-card-text>
      </v-card>

      <p v-if="error" class="login-error" role="alert">{{ error }}</p>
      <div class="settings-actions">
        <button class="submit-button compact" type="button" :disabled="testing || saving" @click="testConnection">{{ testing ? hubLinkCopy["hub.link.testing"] : hubLinkCopy["hub.link.testConnection"] }}</button>
        <button class="submit-button compact" type="submit" :disabled="saving || testing">{{ saving ? hubLinkCopy["hub.link.saving"] : hubLinkCopy["hub.link.save"] }}</button>
        <span v-if="saved" class="success-text" role="status">{{ hubLinkCopy["hub.link.toast.saved"] }}</span>
      </div>
    </form>
    <div v-if="testFeedback" class="hub-link-toast" :class="`hub-link-toast-${testFeedback.kind}`" :role="testFeedback.kind === 'error' ? 'alert' : 'status'" aria-live="polite">
      {{ testFeedback.message }}
    </div>
  </section>
</template>

<style scoped>
.hub-link-page { max-width: 960px; margin-inline: auto; }
.hub-link-status-title { display: flex; align-items: center; justify-content: space-between; gap: 1rem; }
.hub-link-status-copy { grid-column: 1 / -1; margin: 0; }
.hub-link-status-action { grid-column: 1 / -1; margin: -.75rem 0 0; color: var(--creator-muted); }
.hub-secret-field { grid-column: 1 / -1; }
.field-hint { margin: .4rem 0 0; color: var(--creator-muted); font-size: .9rem; }
.rotation-hint { margin-top: .65rem; }
.hub-link-metadata { grid-column: 1 / -1; display: grid; grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr)); gap: .75rem 1.25rem; margin: 0; }
.hub-link-metadata dt { color: var(--creator-muted); font-size: .9rem; }
.hub-link-metadata dd { margin: .2rem 0 0; }
.hub-link-toast { position: fixed; inset-inline-end: 1.5rem; inset-block-end: 1.5rem; z-index: 2000; max-width: min(28rem, calc(100vw - 2rem)); padding: .9rem 1.1rem; border-radius: .65rem; color: #fff; box-shadow: 0 8px 30px #221b3833; }
.hub-link-toast-success { background: #176b48; }
.hub-link-toast-error { background: #b3263e; }
.hub-link-toast-warning { background: #805300; }
</style>
