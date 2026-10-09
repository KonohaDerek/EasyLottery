<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { parse, stringify } from "yaml";
import { apiText } from "../api/client";

type AuditRecord = Record<string, unknown>;
const document = ref<Record<string, any>>({});
const etag = ref("");
const loading = ref(true);
const loaded = ref(false);
const saving = ref(false);
const error = ref("");
const saved = ref(false);

const auditSettings = computed<Record<string, any>>(() => {
  document.value.systemSettings ??= {};
  document.value.systemSettings.audit ??= {};
  return document.value.systemSettings.audit;
});
const records = computed<AuditRecord[]>(() => Array.isArray(document.value.auditRecords) ? document.value.auditRecords : []);

async function load() {
  loading.value = true;
  loaded.value = false;
  error.value = "";
  try {
    const response = await apiText("/settings");
    if (!response.etag) throw new Error("設定載入缺少 ETag，請重新載入後再儲存。");
    document.value = parse(response.data) as Record<string, any>;
    etag.value = response.etag;
    loaded.value = true;
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "審計設定載入失敗。";
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
  saved.value = false;
  error.value = "";
  try {
    const response = await apiText("/settings", {
      method: "PUT",
      headers: etag.value ? { "If-Match": etag.value } : undefined,
      body: stringify(document.value)
    });
    etag.value = response.etag || etag.value;
    saved.value = true;
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "審計設定儲存失敗。";
  } finally {
    saving.value = false;
  }
}

function value(record: AuditRecord, key: string): string {
  const result = record[key];
  return result === null || result === undefined ? "" : String(result);
}

onMounted(load);
</script>

<template>
  <section class="settings-page" aria-labelledby="audit-title">
    <div class="page-heading"><p class="eyebrow">AUDIT</p><h1 id="audit-title">審計紀錄</h1><p class="lead">審計設定沿用既有 YAML 相容 API，儲存時會保留後端產生的完整紀錄與遮罩密鑰。</p></div>
    <v-card class="settings-card">
      <v-card-title>操作人設定</v-card-title>
      <v-card-text>
        <p v-if="loading" class="status-text">載入中…</p>
        <div v-else-if="!loaded" class="login-error" role="alert">
          <p>{{ error || "審計設定尚未載入。" }}</p>
          <button class="submit-button compact" type="button" @click="load">重新載入</button>
        </div>
        <form v-else @submit.prevent="save">
          <label class="field-label" for="audit-actor">預設操作人</label>
          <input id="audit-actor" v-model="auditSettings.actorName" class="settings-input" maxlength="120" />
          <div class="settings-actions"><button class="submit-button compact" type="submit" :disabled="saving">{{ saving ? "儲存中…" : "儲存審計設定" }}</button><span v-if="saved" class="success-text" role="status">已儲存</span></div>
        </form>
      </v-card-text>
    </v-card>
    <v-card class="settings-card">
      <v-card-title>最近紀錄</v-card-title>
      <v-card-text>
        <p v-if="!loaded" class="status-text">審計紀錄尚未載入。</p>
        <p v-else-if="records.length === 0" class="status-text">目前沒有審計紀錄。</p>
        <div v-else class="table-wrap"><table><thead><tr><th>時間</th><th>操作人</th><th>類別</th><th>動作</th><th>目標</th><th>詳細</th></tr></thead><tbody><tr v-for="(record, index) in records" :key="value(record, 'id') || index"><td>{{ value(record, 'changedAtUtc') }}</td><td>{{ value(record, 'changedBy') }}</td><td>{{ value(record, 'category') }}</td><td>{{ value(record, 'action') }}</td><td>{{ value(record, 'targetName') }}</td><td>{{ value(record, 'details') }}</td></tr></tbody></table></div>
      </v-card-text>
    </v-card>
    <div v-if="error && loaded" class="login-error" role="alert">{{ error }}</div>
  </section>
</template>
