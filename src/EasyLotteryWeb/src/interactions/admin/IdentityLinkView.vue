<script setup lang="ts">
import { ref } from "vue";
import { ApiError } from "../../api/client";
import { completeIdentityLink } from "./api";

const code = ref("");
const submitting = ref(false);
const message = ref("");
const messageType = ref<"success" | "error">("success");

async function completeLink(): Promise<void> {
  if (!code.value.trim() || submitting.value) return;
  submitting.value = true;
  message.value = "";
  try {
    await completeIdentityLink(code.value.trim());
    code.value = "";
    messageType.value = "success";
    message.value = "身份綁定完成。一次性綁定碼已清除。";
  } catch (cause) {
    messageType.value = "error";
    if (cause instanceof ApiError && cause.status === 429) {
      message.value = "操作太頻繁，請稍後再試。";
    } else if (cause instanceof ApiError && (cause.status === 401 || cause.status === 403)) {
      message.value = "管理員工作階段已失效，請重新登入後再試。";
    } else if (cause instanceof ApiError && cause.status < 500) {
      message.value = "綁定碼無效、已過期或已使用。請向主持人取得新的綁定碼。";
    } else {
      message.value = "目前無法完成綁定，請確認網路連線後重試。";
    }
  } finally {
    submitting.value = false;
  }
}
</script>

<template>
  <section class="settings-page identity-link-page" aria-labelledby="identity-link-title">
    <header class="page-heading">
      <h1 id="identity-link-title">觀眾身份綁定</h1>
      <p class="lead">合併觀眾在不同平台的身份前，請確認一次性綁定碼來自可信的主持人。</p>
    </header>

    <v-card class="identity-link-card" rounded="xl" variant="outlined">
      <v-card-item>
        <v-card-title>使用一次性綁定碼</v-card-title>
        <v-card-subtitle>綁定碼過期或使用後即失效，不會顯示在網址或錯誤訊息中。</v-card-subtitle>
      </v-card-item>
      <v-card-text>
        <form class="identity-link-form" @submit.prevent="completeLink">
          <v-text-field v-model="code" label="一次性綁定碼" type="password" autocomplete="one-time-code" autocapitalize="off" spellcheck="false" :disabled="submitting" />
          <v-alert v-if="message" :type="messageType" variant="tonal" :role="messageType === 'error' ? 'alert' : 'status'">{{ message }}</v-alert>
          <v-btn color="primary" type="submit" :loading="submitting" :disabled="!code.trim()">完成綁定</v-btn>
        </form>
      </v-card-text>
    </v-card>
  </section>
</template>

<style scoped>
.identity-link-card { max-width: 52rem; border-color: rgba(92, 76, 180, 0.18); }
.identity-link-form { display: grid; gap: 0.8rem; }
</style>
