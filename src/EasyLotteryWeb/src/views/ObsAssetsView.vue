<script setup lang="ts">
import { onMounted, ref } from "vue";
import { apiRequest } from "../api/client";

interface Asset { id: string; fileName: string; contentType: string; kind: string; length: number; createdAtUtc: string; referencedBy: string[]; }
interface Limits { maxAssetBytes: number; maxPackageBytes: number; }
const assets = ref<Asset[]>([]);
const limits = ref<Limits | null>(null);
const kind = ref("Image");
const file = ref<File | null>(null);
const loading = ref(true);
const busy = ref(false);
const error = ref("");
const showUnused = ref(false);

async function load() {
  loading.value = true;
  error.value = "";
  try {
    const [assetResponse, limitResponse] = await Promise.all([
      apiRequest<Asset[]>(showUnused.value ? "/api/obs-assets/unused" : "/api/obs-assets"),
      apiRequest<Limits>("/api/obs-assets/limits")
    ]);
    assets.value = assetResponse.data;
    limits.value = limitResponse.data;
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "OBS 資產載入失敗。";
  } finally {
    loading.value = false;
  }
}

async function upload() {
  if (!file.value) return;
  busy.value = true;
  error.value = "";
  try {
    const form = new FormData();
    form.append("file", file.value);
    form.append("kind", kind.value);
    await apiRequest<Asset>("/api/obs-assets", { method: "POST", body: form });
    file.value = null;
    const input = document.querySelector<HTMLInputElement>("#asset-file");
    if (input) input.value = "";
    await load();
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "OBS 資產上傳失敗。";
  } finally {
    busy.value = false;
  }
}

async function remove(asset: Asset) {
  if (asset.referencedBy.length > 0 || !window.confirm(`確定要刪除 ${asset.fileName} 嗎？`)) return;
  busy.value = true;
  error.value = "";
  try {
    await apiRequest<null>(`/api/obs-assets/${encodeURIComponent(asset.id)}`, { method: "DELETE" });
    await load();
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "OBS 資產刪除失敗。";
  } finally {
    busy.value = false;
  }
}

function bytes(value: number) { return `${Math.max(1, Math.round(value / 1024))} KB`; }
onMounted(load);
</script>

<template>
  <section class="settings-page" aria-labelledby="assets-title">
    <div class="page-heading"><p class="eyebrow">OBS ASSETS</p><h1 id="assets-title">OBS 資產</h1><p class="lead">管理 OBS 可離線使用的圖片、音效與其他檔案。仍被設定引用的資產不可刪除。</p></div>
    <v-card class="settings-card"><v-card-title>上傳資產</v-card-title><v-card-text>
      <form class="inline-form" @submit.prevent="upload"><input id="asset-file" class="settings-input" type="file" @change="file = ($event.target as HTMLInputElement).files?.[0] ?? null" /><select v-model="kind" class="settings-input"><option>Image</option><option>Audio</option><option>Video</option><option>Model</option><option>Other</option></select><button class="submit-button compact" type="submit" :disabled="busy || !file">{{ busy ? "上傳中…" : "上傳" }}</button></form>
      <p v-if="limits" class="muted-text">單檔上限 {{ bytes(limits.maxAssetBytes) }}；匯出套件上限 {{ bytes(limits.maxPackageBytes) }}。</p>
    </v-card-text></v-card>
    <v-card class="settings-card"><v-card-title>資產清單 <label class="checkbox-label heading-checkbox"><input v-model="showUnused" type="checkbox" @change="load" /> 只顯示未使用</label></v-card-title><v-card-text>
      <p v-if="loading" class="status-text">載入中…</p><p v-else-if="assets.length === 0" class="status-text">目前沒有資產。</p>
      <div v-else class="table-wrap"><table><thead><tr><th>檔名</th><th>類型</th><th>大小</th><th>引用</th><th>操作</th></tr></thead><tbody><tr v-for="asset in assets" :key="asset.id"><td>{{ asset.fileName }}</td><td>{{ asset.kind }}</td><td>{{ bytes(asset.length) }}</td><td>{{ asset.referencedBy.length ? asset.referencedBy.join(', ') : '未使用' }}</td><td><button class="danger-button" type="button" :disabled="busy || asset.referencedBy.length > 0" @click="remove(asset)">刪除</button></td></tr></tbody></table></div>
      <div v-if="error" class="login-error" role="alert">{{ error }}</div>
    </v-card-text></v-card>
  </section>
</template>
