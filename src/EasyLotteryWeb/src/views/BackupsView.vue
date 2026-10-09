<script setup lang="ts">
import { onMounted, ref } from "vue";
import { apiRequest } from "../api/client";

interface Backup { id: string; createdAtUtc: string; length: number; }
const backups = ref<Backup[]>([]);
const loading = ref(true);
const busyId = ref("");
const error = ref("");

async function load() {
  loading.value = true;
  error.value = "";
  try {
    backups.value = (await apiRequest<Backup[]>("/api/settings/backups")).data;
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "備份清單載入失敗。";
  } finally {
    loading.value = false;
  }
}

async function restore(backup: Backup) {
  if (!window.confirm(`確定要回復 ${backup.id} 嗎？`)) return;
  busyId.value = backup.id;
  error.value = "";
  try {
    await apiRequest<null>(`/api/settings/backups/${encodeURIComponent(backup.id)}/restore`, { method: "POST" });
    await load();
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "備份回復失敗。";
  } finally {
    busyId.value = "";
  }
}

function size(bytes: number) { return `${Math.max(1, Math.round(bytes / 1024))} KB`; }
onMounted(load);
</script>

<template>
  <section class="settings-page" aria-labelledby="backups-title">
    <div class="page-heading"><p class="eyebrow">BACKUPS</p><h1 id="backups-title">設定備份與回復</h1><p class="lead">回復會由後端建立目前設定的保護性備份，完成後重新載入清單。</p></div>
    <v-card class="settings-card"><v-card-text>
      <p v-if="loading" class="status-text">載入中…</p>
      <p v-else-if="backups.length === 0" class="status-text">目前沒有可用備份。</p>
      <div v-else class="table-wrap"><table><thead><tr><th>備份</th><th>建立時間</th><th>大小</th><th>操作</th></tr></thead><tbody><tr v-for="backup in backups" :key="backup.id"><td>{{ backup.id }}</td><td>{{ new Date(backup.createdAtUtc).toLocaleString() }}</td><td>{{ size(backup.length) }}</td><td><button class="danger-button" type="button" :disabled="Boolean(busyId)" @click="restore(backup)">{{ busyId === backup.id ? "回復中…" : "回復" }}</button></td></tr></tbody></table></div>
      <div v-if="error" class="login-error" role="alert">{{ error }}</div>
    </v-card-text></v-card>
  </section>
</template>
