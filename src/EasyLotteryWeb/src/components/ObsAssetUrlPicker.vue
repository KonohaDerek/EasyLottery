<script setup lang="ts">
import { ref } from "vue";
import { apiRequest } from "../api/client";

interface ObsAsset {
  id: string;
  fileName: string;
  contentType: string;
  kind: number;
  length: number;
}

const props = defineProps<{
  modelValue: string;
  kind: 0 | 1 | 4;
  label: string;
  placeholder: string;
}>();
const emit = defineEmits<{ "update:modelValue": [value: string] }>();

const dialog = ref<HTMLDialogElement | null>(null);
const assets = ref<ObsAsset[]>([]);
const loading = ref(false);
const error = ref("");

async function openPicker() {
  error.value = "";
  loading.value = true;
  if (dialog.value && !dialog.value.open) dialog.value.showModal();
  try {
    const response = await apiRequest<ObsAsset[]>("/api/obs-assets");
    assets.value = response.data.filter(asset => asset.kind === props.kind);
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "OBS 資產載入失敗。";
  } finally {
    loading.value = false;
  }
}

function select(asset: ObsAsset) {
  emit("update:modelValue", `/api/obs-assets/${encodeURIComponent(asset.id)}/content`);
  dialog.value?.close();
}

function formatSize(bytes: number) {
  if (bytes >= 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  if (bytes >= 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${bytes} B`;
}
</script>

<template>
  <div class="asset-picker">
    <div class="asset-input-row">
      <input
        :aria-label="label"
        :placeholder="placeholder"
        :value="modelValue"
        class="settings-input"
        @input="emit('update:modelValue', ($event.target as HTMLInputElement).value)"
      />
      <button class="secondary-button" type="button" @click="openPicker">選擇資產</button>
    </div>
    <small v-if="modelValue.includes('/api/obs-assets/')" class="local-asset-hint">已使用本機 OBS 資產</small>
    <dialog ref="dialog" class="asset-dialog" aria-label="選擇 OBS 資產">
      <header class="dialog-heading">
        <h2>選擇{{ kind === 0 ? "圖片" : kind === 1 ? "音訊" : "影片" }}</h2>
        <button class="icon-button" type="button" aria-label="關閉" @click="dialog?.close()">×</button>
      </header>
      <div class="dialog-content">
        <p v-if="loading" role="status">載入資產中…</p>
        <div v-else-if="error" class="login-error" role="alert">
          {{ error }} <button class="secondary-button" type="button" @click="openPicker">重試</button>
        </div>
        <p v-else-if="assets.length === 0" class="muted-text">尚無符合用途的本機資產，請先至 OBS 資產庫上傳。</p>
        <div v-else class="asset-grid">
          <button v-for="asset in assets" :key="asset.id" class="asset-choice" type="button" @click="select(asset)">
            <img v-if="kind === 0" :src="`/api/obs-assets/${encodeURIComponent(asset.id)}/content`" :alt="asset.fileName" loading="lazy" />
            <audio v-else-if="kind === 1" :src="`/api/obs-assets/${encodeURIComponent(asset.id)}/content`" controls preload="metadata" :aria-label="asset.fileName" />
            <video v-else :src="`/api/obs-assets/${encodeURIComponent(asset.id)}/content`" muted preload="metadata" :aria-label="asset.fileName" />
            <strong>{{ asset.fileName }}</strong>
            <small>{{ formatSize(asset.length) }}</small>
          </button>
        </div>
      </div>
    </dialog>
  </div>
</template>

<style scoped>
.asset-picker { display: grid; gap: .3rem; }
.asset-input-row { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: .4rem; }
.local-asset-hint { color: #166534; }
.asset-dialog { width: min(900px, calc(100vw - 2rem)); max-height: min(80vh, 720px); padding: 0; border: 0; border-radius: .8rem; box-shadow: 0 20px 60px #0f172a55; }
.asset-dialog::backdrop { background: #0f172a88; }
.dialog-heading { display: flex; align-items: center; justify-content: space-between; padding: 1rem 1.25rem; border-bottom: 1px solid #e5eaf2; }
.dialog-heading h2 { margin: 0; font-size: 1.2rem; }
.dialog-content { min-height: 5rem; padding: 1.25rem; overflow: auto; }
.icon-button { font: inherit; font-size: 1.5rem; border: 0; background: transparent; cursor: pointer; }
.asset-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(145px, 1fr)); gap: .75rem; }
.asset-choice { display: grid; gap: .4rem; padding: .6rem; text-align: left; color: #1f2937; background: white; border: 1px solid #dbe3ef; border-radius: .5rem; cursor: pointer; }
.asset-choice:hover { border-color: #2563eb; background: #eff6ff; }
.asset-choice img, .asset-choice video { width: 100%; height: 105px; object-fit: contain; background: #f1f5f9; }
.asset-choice audio { width: 100%; }
.asset-choice small { color: #64748b; }
.secondary-button { min-height: 2.65rem; padding: .5rem .75rem; color: #1e40af; font: inherit; background: #eff6ff; border: 1px solid #bfdbfe; border-radius: .35rem; cursor: pointer; }
@media (max-width: 480px) { .asset-input-row { grid-template-columns: 1fr; } }
</style>
