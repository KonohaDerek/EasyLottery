<template>
  <section class="login-page" aria-labelledby="admin-login-title">
    <v-card class="login-card mx-auto" max-width="560">
      <v-card-item>
        <v-card-title id="admin-login-title" class="text-h4">管理員 Passkey 登入</v-card-title>
        <v-card-subtitle>EasyLottery 只允許設定的管理員 email 使用。</v-card-subtitle>
      </v-card-item>

      <v-card-text>
        <form novalidate @submit.prevent="submit">
          <label class="field-label" for="admin-email">管理員 email</label>
          <input
            id="admin-email"
            v-model="email"
            class="email-input"
            autocomplete="username"
            type="email"
            :disabled="busy"
          />
          <p class="text-body-2 text-medium-emphasis">Passkey 私鑰會留在你的裝置或密碼管理器。</p>
          <div v-if="error" class="login-error" role="alert">{{ error }}</div>
          <button class="submit-button" type="submit" :disabled="busy" :aria-busy="busy">
            使用 Passkey 登入
          </button>
        </form>
      </v-card-text>
    </v-card>
  </section>
</template>

<script setup lang="ts">
import { ref } from "vue";
import { useRoute } from "vue-router";
import { isValidEmail } from "../auth/email";
import { loginWithPasskey } from "../auth/passkey";
import { safePostLoginRedirect } from "../auth/redirect";
import { useSessionStore } from "../stores/session";

const email = ref("");
const error = ref("");
const busy = ref(false);
const session = useSessionStore();
const route = useRoute();

async function submit() {
  error.value = "";
  const normalizedEmail = email.value.trim();
  if (!isValidEmail(normalizedEmail)) {
    error.value = "請輸入有效的管理員 email。";
    return;
  }

  busy.value = true;
  try {
    const result = await loginWithPasskey(normalizedEmail);
    session.setToken(result.token);
    const requestedRedirect = typeof route.query.redirect === "string" ? route.query.redirect : undefined;
    window.location.assign(safePostLoginRedirect(requestedRedirect, window.location.origin));
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "Passkey 登入失敗，請稍後再試。";
  } finally {
    busy.value = false;
  }
}
</script>
