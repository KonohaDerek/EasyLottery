<script setup lang="ts">
import { onMounted, ref } from "vue";
import { addPasskey, listPasskeys, removePasskey, type PasskeyDevice } from "../auth/passkey";

const passkeys = ref<PasskeyDevice[]>([]);
const newPasskeyName = ref("Admin Passkey");
const loading = ref(true);
const busy = ref(false);
const error = ref("");

async function load() {
  loading.value = true;
  error.value = "";
  try {
    passkeys.value = await listPasskeys();
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "Passkey 載入失敗。";
  } finally {
    loading.value = false;
  }
}

async function add() {
  busy.value = true;
  error.value = "";
  try {
    await addPasskey(newPasskeyName.value.trim() || "Admin Passkey");
    newPasskeyName.value = "Admin Passkey";
    await load();
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "Passkey 新增失敗。";
  } finally {
    busy.value = false;
  }
}

async function remove(passkey: PasskeyDevice) {
  if (passkeys.value.length <= 1 || !window.confirm(`確定要移除「${passkey.name}」嗎？`)) return;
  busy.value = true;
  error.value = "";
  try {
    await removePasskey(passkey.id);
    await load();
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "Passkey 移除失敗。";
  } finally {
    busy.value = false;
  }
}

onMounted(load);
</script>

<template>
  <section class="settings-page" aria-labelledby="admin-access-title">
    <div class="page-heading">
      <p class="eyebrow">ADMIN ACCESS</p>
      <h1 id="admin-access-title">管理員 Passkey 管理</h1>
      <p class="lead">Passkey 私鑰只會留在裝置或密碼管理器，API 只保存公開金鑰與簽章計數。</p>
    </div>

    <v-card class="settings-card">
      <v-card-title>新增 Passkey 裝置</v-card-title>
      <v-card-text>
        <form class="inline-form" @submit.prevent="add">
          <div>
            <label class="field-label" for="passkey-name">裝置名稱</label>
            <input id="passkey-name" v-model="newPasskeyName" class="settings-input" maxlength="80" :disabled="busy" />
          </div>
          <button class="submit-button compact" type="submit" :disabled="busy">{{ busy ? "處理中…" : "新增 Passkey" }}</button>
        </form>
        <p class="muted-text">至少保留一個 Passkey，避免移除最後一個裝置後無法登入。</p>
      </v-card-text>
    </v-card>

    <v-card class="settings-card">
      <v-card-title>已註冊的 Passkey</v-card-title>
      <v-card-text>
        <p v-if="loading" class="status-text">載入中…</p>
        <p v-else-if="passkeys.length === 0" class="status-text">目前沒有 Passkey。</p>
        <div v-else class="table-wrap">
          <table>
            <thead><tr><th>裝置</th><th>建立時間</th><th>操作</th></tr></thead>
            <tbody>
              <tr v-for="passkey in passkeys" :key="passkey.id">
                <td>{{ passkey.name }}</td>
                <td>{{ new Date(passkey.createdAtUtc).toLocaleString() }}</td>
                <td><button class="danger-button" type="button" :disabled="busy || passkeys.length <= 1" @click="remove(passkey)">移除</button></td>
              </tr>
            </tbody>
          </table>
        </div>
        <div v-if="error" class="login-error" role="alert">{{ error }}</div>
      </v-card-text>
    </v-card>
  </section>
</template>
