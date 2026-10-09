<script setup lang="ts">
import { onMounted, ref } from "vue";
import { type PaymentSettings, emptyPaymentSettings, loadPaymentSettings, savePaymentSettings } from "../api/settings";

const settings = ref<PaymentSettings>(emptyPaymentSettings());
const etag = ref("");
const loading = ref(true);
const saving = ref(false);
const error = ref("");
const saved = ref(false);
const providers = [
  ["ecpay", "綠界"],
  ["newebPay", "藍新"],
  ["oenTw", "oen.tw"],
  ["twitchBits", "Twitch 小奇點"]
] as const;

function providerSettings(key: string) {
  settings.value.donationIntegration[key] ??= {};
  return settings.value.donationIntegration[key];
}

function youtubeSettings() {
  settings.value.youTube ??= {};
  return settings.value.youTube;
}

function text(section: Record<string, unknown>, key: string): string {
  return typeof section[key] === "string" ? section[key] as string : "";
}

function numberValue(section: Record<string, unknown>, key: string, fallback: number): number {
  return typeof section[key] === "number" ? section[key] as number : fallback;
}

async function load() {
  loading.value = true;
  error.value = "";
  try {
    const response = await loadPaymentSettings();
    settings.value = response.data;
    etag.value = response.etag;
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "支付設定載入失敗。";
  } finally {
    loading.value = false;
  }
}

async function save() {
  saving.value = true;
  saved.value = false;
  error.value = "";
  try {
    const response = await savePaymentSettings(settings.value, etag.value);
    settings.value = response.data;
    etag.value = response.etag || etag.value;
    saved.value = true;
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "支付設定儲存失敗。";
  } finally {
    saving.value = false;
  }
}

onMounted(load);
</script>

<template>
  <section class="settings-page" aria-labelledby="payment-title">
    <div class="page-heading"><p class="eyebrow">PAYMENT SETTINGS</p><h1 id="payment-title">支付配置</h1><p class="lead">保留既有支付 API 契約與遮罩密鑰，修改後由後端驗證並寫入設定。</p></div>
    <p v-if="loading" class="status-text">載入中…</p>
    <form v-else class="settings-form" @submit.prevent="save">
      <v-card class="settings-card">
        <v-card-title>公開回呼與通知</v-card-title>
        <v-card-text class="form-grid">
          <div><label class="field-label" for="payment-domain">自訂網域</label><input id="payment-domain" v-model="settings.publicCallback.customDomain" class="settings-input" /></div>
          <div><label class="field-label" for="payment-tunnel">Tunnel Provider</label><select id="payment-tunnel" v-model="settings.publicCallback.tunnelProvider" class="settings-input"><option value="cloudflare-quick">Cloudflare Quick</option><option value="dev-tunnels">Dev Tunnels</option></select></div>
          <div><label class="field-label" for="payment-base-url">目前公開 URL</label><input id="payment-base-url" v-model="settings.publicCallback.activePublicBaseUrl" class="settings-input" /></div>
          <div><label class="field-label" for="notification-email">結果通知 email</label><input id="notification-email" v-model="settings.resultNotificationEmail" class="settings-input" type="email" /></div>
        </v-card-text>
      </v-card>

      <v-card class="settings-card">
        <v-card-title>Mail SMTP</v-card-title>
        <v-card-text class="form-grid">
          <div><label class="field-label" for="smtp-host">SMTP Host</label><input id="smtp-host" v-model="settings.mailDelivery.smtpHost" class="settings-input" /></div>
          <div><label class="field-label" for="smtp-port">SMTP Port</label><input id="smtp-port" :value="numberValue(settings.mailDelivery, 'smtpPort', 587)" class="settings-input" type="number" @input="settings.mailDelivery.smtpPort = Number(($event.target as HTMLInputElement).value)" /></div>
          <div><label class="field-label" for="smtp-user">SMTP Username</label><input id="smtp-user" :value="text(settings.mailDelivery, 'smtpUsername')" class="settings-input" @input="settings.mailDelivery.smtpUsername = ($event.target as HTMLInputElement).value" /></div>
          <div><label class="field-label" for="smtp-password">SMTP Password</label><input id="smtp-password" :value="text(settings.mailDelivery, 'smtpPassword')" class="settings-input" type="password" autocomplete="new-password" @input="settings.mailDelivery.smtpPassword = ($event.target as HTMLInputElement).value" /></div>
          <div><label class="field-label" for="from-address">寄件地址</label><input id="from-address" :value="text(settings.mailDelivery, 'fromAddress')" class="settings-input" type="email" @input="settings.mailDelivery.fromAddress = ($event.target as HTMLInputElement).value" /></div>
          <div><label class="field-label" for="from-name">寄件名稱</label><input id="from-name" :value="text(settings.mailDelivery, 'fromName')" class="settings-input" @input="settings.mailDelivery.fromName = ($event.target as HTMLInputElement).value" /></div>
        </v-card-text>
      </v-card>

      <v-card class="settings-card">
        <v-card-title>付款提供者</v-card-title>
        <v-card-text>
          <div v-for="[key, label] in providers" :key="key" class="provider-row">
            <div><strong>{{ label }}</strong><small>{{ providerSettings(key).environment === 'production' ? '正式環境' : '測試環境' }}</small></div>
            <select :value="providerSettings(key).environment ?? 'testing'" class="settings-input provider-environment" @change="providerSettings(key).environment = ($event.target as HTMLSelectElement).value"><option value="testing">測試</option><option value="production">正式</option></select>
            <label class="checkbox-label"><input type="checkbox" :checked="providerSettings(key).isEnabled === true" @change="providerSettings(key).isEnabled = ($event.target as HTMLInputElement).checked" /> 啟用</label>
          </div>
          <label class="checkbox-label"><input v-model="settings.enableYouTubeSuperChat" type="checkbox" /> 啟用 YouTube Super Chat</label>
          <div class="form-grid single-margin"><label class="field-label" for="youtube-key">YouTube API Key（遮罩值會原樣保留）</label><input id="youtube-key" :value="text(youtubeSettings(), 'apiKey')" class="settings-input" type="password" autocomplete="new-password" @input="youtubeSettings().apiKey = ($event.target as HTMLInputElement).value" /></div>
        </v-card-text>
      </v-card>

      <div class="settings-actions"><button class="submit-button compact" type="submit" :disabled="saving">{{ saving ? "儲存中…" : "儲存支付設定" }}</button><span v-if="saved" class="success-text" role="status">已儲存</span></div>
    </form>
    <div v-if="error" class="login-error" role="alert">{{ error }}</div>
  </section>
</template>
