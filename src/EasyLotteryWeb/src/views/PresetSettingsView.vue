<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { apiRequest } from "../api/client";

interface Preset {
  key: string;
  label: string;
}

const props = defineProps<{
  title: string;
  endpoint: string;
  valueKey: string;
  fieldLabel: string;
  presets: Preset[];
  imageFields?: boolean;
}>();

const settings = reactive<Record<string, string>>({
  [props.valueKey]: "",
  backgroundImageUrl: "",
  bannerImageUrl: ""
});
const etag = ref("");
const loading = ref(true);
const saving = ref(false);
const error = ref("");
const saved = ref(false);

async function load() {
  loading.value = true;
  error.value = "";
  try {
    const response = await apiRequest<Record<string, string>>(props.endpoint);
    Object.assign(settings, response.data);
    etag.value = response.etag;
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "設定載入失敗。";
  } finally {
    loading.value = false;
  }
}

async function save() {
  saving.value = true;
  error.value = "";
  saved.value = false;
  try {
    const response = await apiRequest<Record<string, string>>(props.endpoint, {
      method: "PUT",
      headers: etag.value ? { "If-Match": etag.value } : undefined,
      body: JSON.stringify(settings)
    });
    Object.assign(settings, response.data);
    etag.value = response.etag || etag.value;
    saved.value = true;
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "設定儲存失敗。";
  } finally {
    saving.value = false;
  }
}

onMounted(load);
</script>

<template>
  <section class="settings-page" :aria-labelledby="`${valueKey}-title`">
    <div class="page-heading">
      <p class="eyebrow">ADMIN SETTINGS</p>
      <h1 :id="`${valueKey}-title`">{{ title }}</h1>
      <p class="lead">設定會透過既有 API 儲存，並以 ETag 避免覆蓋其他管理員剛更新的內容。</p>
    </div>

    <v-card class="settings-card">
      <v-card-title>{{ fieldLabel }}</v-card-title>
      <v-card-text>
        <p v-if="loading" class="status-text">載入中…</p>
        <form v-else @submit.prevent="save">
          <div class="preset-grid">
            <label v-for="preset in presets" :key="preset.key" class="preset-option">
              <input v-model="settings[valueKey]" type="radio" :value="preset.key" />
              <span><strong>{{ preset.label }}</strong><small>{{ preset.key }}</small></span>
            </label>
          </div>
          <template v-if="imageFields">
            <label class="field-label" :for="`${valueKey}-background`">背景圖片 URL</label>
            <input :id="`${valueKey}-background`" v-model="settings.backgroundImageUrl" class="settings-input" type="url" />
            <label class="field-label" :for="`${valueKey}-banner`">Banner 圖片 URL</label>
            <input :id="`${valueKey}-banner`" v-model="settings.bannerImageUrl" class="settings-input" type="url" />
          </template>
          <div class="settings-actions">
            <button class="submit-button compact" type="submit" :disabled="saving">{{ saving ? "儲存中…" : "儲存設定" }}</button>
            <span v-if="saved" class="success-text" role="status">已儲存</span>
          </div>
        </form>
        <div v-if="error" class="login-error" role="alert">{{ error }}</div>
      </v-card-text>
    </v-card>
  </section>
</template>
