<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()
const email = ref('')

async function signIn(): Promise<void> {
  try {
    await auth.login(email.value.trim())
    const requestedPath = typeof route.query.redirect === 'string' ? route.query.redirect : ''
    const safePath = requestedPath.startsWith('/') && !requestedPath.startsWith('//')
      ? requestedPath
      : '/system/access'
    await router.replace(safePath)
  } catch {
    // The form renders the store's error while keeping the email available for correction.
  }
}
</script>

<template>
  <main class="page-content login-page" aria-labelledby="login-title">
    <v-card class="content-card login-card" variant="flat">
      <div class="login-emblem" aria-hidden="true">
        <v-icon icon="mdi-shield-key-outline" size="30" />
      </div>
      <p class="eyebrow">管理員安全登入</p>
      <h1 id="login-title" class="page-title">使用 Passkey 登入</h1>
      <p class="page-lead login-lead">
        使用已註冊的 Passkey 驗證。如果這是此站台第一次設定，驗證流程會為管理員註冊第一個 Passkey。
      </p>

      <form class="login-form" @submit.prevent="signIn">
        <label class="field-label" for="admin-email">管理員 email</label>
        <v-text-field
          id="admin-email"
          v-model="email"
          class="email-field"
          autocomplete="username"
          type="email"
          placeholder="name@example.com"
          required
          maxlength="254"
          variant="outlined"
          density="comfortable"
          hide-details="auto"
          aria-describedby="login-help"
          :disabled="auth.isBusy"
        />
        <p id="login-help" class="field-help">
          Passkey 私密金鑰不會傳送至 EasyLottery 伺服器。
        </p>

        <v-alert
          v-if="auth.error"
          class="login-error"
          type="error"
          variant="tonal"
          role="alert"
        >
          {{ auth.error }}
        </v-alert>

        <v-btn
          block
          color="primary"
          size="large"
          type="submit"
          prepend-icon="mdi-key-chain-variant"
          :loading="auth.isBusy"
          :disabled="auth.isBusy"
        >
          使用 Passkey 登入
        </v-btn>
      </form>

      <p class="login-footnote">
        已登入的管理員可在「Passkey 管理」新增或移除裝置。請至少保留一個可用的 Passkey。
      </p>
    </v-card>
  </main>
</template>

<style scoped>
.login-page {
  display: grid;
  min-height: calc(100vh - 12rem);
  place-items: center;
}

.login-card {
  width: min(100%, 34rem);
  padding: clamp(1.5rem, 5vw, 2.5rem);
}

.login-emblem {
  display: grid;
  width: 3.5rem;
  height: 3.5rem;
  place-items: center;
  margin-bottom: 1.4rem;
  border-radius: 1.1rem;
  background: #e8f2ec;
  color: #145c4b;
}

.login-card .page-title {
  font-size: clamp(1.8rem, 5vw, 2.4rem);
}

.login-lead {
  margin-top: 0.7rem;
  font-size: 1rem;
}

.login-form {
  display: grid;
  gap: 0.5rem;
  margin-top: 1.8rem;
}

.field-label {
  color: #244d40;
  font-size: 0.95rem;
  font-weight: 600;
}

.email-field {
  margin-top: 0.2rem;
}

.field-help,
.login-footnote {
  color: #63776f;
  font-size: 0.88rem;
  line-height: 1.6;
}

.field-help {
  margin: -0.5rem 0 0.65rem;
}

.login-error {
  margin-bottom: 0.65rem;
}

.login-footnote {
  margin: 1.35rem 0 0;
}
</style>
