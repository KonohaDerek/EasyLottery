<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { ApiError } from "../api/client";
import { cardHubMessages as copy } from "../card-hub/messages";
import {
  loadCardHubLink,
  loadShopFeatureStatus,
  saveCardHubLink,
  testCardHubLink,
  unlinkCardHub,
  type CardHubLinkSettings,
  type CardHubLinkStatus
} from "../card-hub/api";

const form = reactive({ hubUrl: "", siteClientId: "", clientSecret: "" });
const saved = ref<CardHubLinkSettings | null>(null);
const shopEnabled = ref(false);
const loading = ref(true);
const saving = ref(false);
const testing = ref(false);
const error = ref("");
const notice = ref("");
const unlinkDialog = ref(false);

const hasSecret = computed(() => saved.value?.hasSecret === true);
const canSave = computed(() => !saving.value && Boolean(form.hubUrl.trim() && form.siteClientId.trim() && (form.clientSecret || hasSecret.value)));

function statusTitle(status: CardHubLinkStatus): string {
  return status === "linked" ? copy["hub.link.status.linked"]
    : status === "credentials_invalid" ? copy["hub.link.status.invalid"]
      : status === "unreachable" ? copy["hub.link.status.unreachable"]
        : copy["hub.link.status.notLinked"];
}

function statusMessage(status: CardHubLinkStatus): string {
  return status === "linked"
    ? copy["hub.link.status.linked.action"]
    : status === "credentials_invalid" ? copy["hub.link.status.invalid.desc"]
      : status === "unreachable" ? copy["hub.link.status.unreachable.desc"]
        : copy["hub.link.status.notLinked.desc"];
}

function statusColor(status: CardHubLinkStatus): string {
  return status === "linked" ? "success" : status === "unreachable" ? "warning" : status === "credentials_invalid" ? "error" : "default";
}

function showError(cause: unknown, fallback: string): void {
  error.value = cause instanceof ApiError ? cause.message : fallback;
}

async function reload(): Promise<void> {
  loading.value = true;
  error.value = "";
  try {
    const [settings, feature] = await Promise.all([loadCardHubLink(), loadShopFeatureStatus()]);
    saved.value = settings;
    shopEnabled.value = feature.enabled;
    form.hubUrl = settings.hubUrl;
    form.siteClientId = settings.siteClientId;
    form.clientSecret = "";
  } catch (cause) {
    showError(cause, copy["hub.link.error.load"]);
  } finally {
    loading.value = false;
  }
}

async function save(): Promise<void> {
  if (!canSave.value) return;
  saving.value = true;
  error.value = "";
  notice.value = "";
  try {
    saved.value = await saveCardHubLink({ ...form });
    form.clientSecret = "";
    notice.value = saved.value.status === "linked" ? copy["hub.link.toast.saved"]
      : saved.value.status === "credentials_invalid" ? copy["hub.link.toast.saveInvalid"]
        : copy["hub.link.toast.saveUnreachable"];
  } catch (cause) {
    showError(cause, copy["hub.link.error.save"]);
  } finally {
    saving.value = false;
  }
}

async function testConnection(): Promise<void> {
  if (!saved.value?.hasSecret || testing.value) return;
  testing.value = true;
  error.value = "";
  notice.value = "";
  try {
    saved.value = await testCardHubLink();
    notice.value = saved.value.status === "linked" ? copy["hub.link.toast.testSuccess"]
      : saved.value.status === "credentials_invalid" ? copy["hub.link.toast.testInvalid"]
        : copy["hub.link.toast.testUnreachable"];
  } catch (cause) {
    showError(cause, copy["hub.link.error.test"]);
  } finally {
    testing.value = false;
  }
}

async function confirmUnlink(): Promise<void> {
  unlinkDialog.value = false;
  error.value = "";
  try {
    await unlinkCardHub();
    saved.value = { status: "not_linked", hubUrl: "", siteClientId: "", hasSecret: false, tenantId: null, tenantName: null, lastCheckedAtUtc: null, lastPublishAtUtc: null };
    form.hubUrl = "";
    form.siteClientId = "";
    form.clientSecret = "";
    notice.value = copy["hub.link.toast.unlinked"];
  } catch (cause) {
    showError(cause, copy["hub.link.error.unlink"]);
  }
}

function formatDate(value: string | null | undefined): string {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : new Intl.DateTimeFormat("zh-TW", { dateStyle: "medium", timeStyle: "short" }).format(date);
}

onMounted(() => void reload());
</script>

<template>
  <main class="settings-page card-hub-link-page" aria-labelledby="card-hub-title">
    <header class="page-heading">
      <p class="eyebrow">CARD HUB</p>
      <h1 id="card-hub-title">{{ copy["hub.link.title"] }}</h1>
      <p class="lead">{{ copy["hub.link.lead"] }}</p>
    </header>

    <v-alert v-if="!shopEnabled" type="info" variant="tonal" class="mb-4">
      {{ copy["hub.link.shopDisabled"] }}
    </v-alert>
    <v-alert v-if="error" type="error" variant="tonal" role="alert" class="mb-4">{{ error }}</v-alert>
    <p v-if="notice" class="success-text mb-4" role="status">{{ notice }}</p>
    <div v-if="loading" class="state-card" role="status">{{ copy["hub.link.loading"] }}</div>

    <template v-else>
      <v-card v-if="saved" class="connection-card mb-4" rounded="xl" elevation="1">
        <v-card-item>
          <v-card-title>{{ copy["hub.link.section.status"] }}</v-card-title>
          <template #append><v-chip :color="statusColor(saved.status)" size="small" variant="tonal">{{ statusTitle(saved.status) }}</v-chip></template>
        </v-card-item>
        <v-card-text>
          <p role="status">{{ statusMessage(saved.status) }}</p>
          <p v-if="saved.status === 'credentials_invalid'" class="mt-2">{{ copy["hub.link.status.invalid.action"] }}</p>
          <p v-if="saved.status === 'unreachable'" class="mt-2">{{ copy["hub.link.status.unreachable.action"] }}</p>
          <div class="card-hub-metadata mt-3">
            <span>{{ copy["hub.link.field.lastCheck.label"] }}：{{ formatDate(saved.lastCheckedAtUtc) }}</span>
            <span>{{ copy["hub.link.field.lastPublish.label"] }}：{{ formatDate(saved.lastPublishAtUtc) }}</span>
          </div>
          <v-btn v-if="saved.hasSecret" class="mt-3" variant="tonal" :loading="testing" @click="testConnection">
            {{ testing ? copy["hub.link.testing"] : copy["hub.link.testConnection"] }}
          </v-btn>
        </v-card-text>
      </v-card>

      <v-card class="connection-card" rounded="xl" elevation="1">
        <v-card-text>
          <v-text-field v-model="form.hubUrl" :label="copy['hub.link.field.hubUrl.label']" :hint="copy['hub.link.field.hubUrl.helper']" persistent-hint variant="outlined" autocomplete="url" />
          <v-text-field v-model="form.siteClientId" :label="copy['hub.link.field.clientId.label']" :hint="copy['hub.link.field.clientId.helper']" persistent-hint variant="outlined" autocomplete="off" />
          <v-text-field
            v-model="form.clientSecret"
            :label="copy['hub.link.field.clientSecret.label']"
            :placeholder="hasSecret ? copy['hub.link.field.clientSecret.placeholderSaved'] : copy['hub.link.field.clientSecret.placeholderNew']"
            :hint="copy['hub.link.field.clientSecret.helper']"
            persistent-hint
            type="password"
            autocomplete="new-password"
            data-testid="card-hub-client-secret"
          />
          <v-btn color="primary" :disabled="!canSave" :loading="saving" data-testid="card-hub-save" @click="save">
            {{ saving ? copy["hub.link.saving"] : copy["hub.link.save"] }}
          </v-btn>
          <v-btn v-if="hasSecret" class="ml-2" color="error" variant="text" @click="unlinkDialog = true">{{ copy["hub.link.unlink"] }}</v-btn>
        </v-card-text>
      </v-card>
    </template>

    <v-dialog v-model="unlinkDialog" max-width="460">
      <v-card>
        <v-card-title>{{ copy["hub.link.unlink.title"] }}</v-card-title>
        <v-card-text>{{ copy["hub.link.unlink.confirm"] }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="unlinkDialog = false">{{ copy["hub.link.unlink.cancel"] }}</v-btn>
          <v-btn color="error" @click="confirmUnlink">{{ copy["hub.link.unlink.confirmAction"] }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </main>
</template>

<style scoped>
.card-hub-metadata { display: flex; flex-wrap: wrap; gap: 1rem; color: rgb(var(--v-theme-on-surface-variant)); }
</style>
