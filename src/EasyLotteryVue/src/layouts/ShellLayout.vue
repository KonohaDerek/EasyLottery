<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'

import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const auth = useAuthStore()
const mobileDrawerOpen = ref(false)

const navigationItems = computed(() => [
  { title: '新版首頁', icon: 'mdi-view-dashboard-outline', to: { name: 'home' } },
  { title: '關於 EasyLottery', icon: 'mdi-information-outline', to: { name: 'about' } },
  { title: '隱私權政策', icon: 'mdi-shield-account-outline', to: { name: 'privacy' } },
  ...(auth.isAuthenticated
    ? [{ title: 'Passkey 管理', icon: 'mdi-key-chain-variant', to: { name: 'passkeys' } }]
    : [{ title: '管理員登入', icon: 'mdi-login', to: { name: 'login' } }]),
])

async function signOut(): Promise<void> {
  await auth.logout()
  await router.push({ name: 'home' })
}
</script>

<template>
  <v-app>
    <v-app-bar class="app-toolbar" color="surface" flat>
      <v-app-bar-nav-icon
        class="d-md-none"
        aria-label="開啟導覽選單"
        :aria-expanded="mobileDrawerOpen"
        @click="mobileDrawerOpen = true"
      />

      <v-btn class="brand-button" variant="text" :to="{ name: 'home' }" aria-label="EasyLottery 首頁">
        <v-avatar color="primary" size="38" class="brand-mark">
          <span aria-hidden="true">EL</span>
        </v-avatar>
        <span class="brand-name">EasyLottery</span>
      </v-btn>

      <v-spacer />

      <div class="toolbar-links d-none d-md-flex" aria-label="快速導覽">
        <v-btn variant="text" :to="{ name: 'about' }">關於</v-btn>
        <v-btn variant="text" :to="{ name: 'privacy' }">隱私權</v-btn>
        <v-btn v-if="auth.isAuthenticated" variant="text" :to="{ name: 'passkeys' }">
          管理員設定
        </v-btn>
        <v-btn v-else variant="text" :to="{ name: 'login' }">管理員登入</v-btn>
      </div>

      <v-btn class="legacy-link" variant="outlined" href="/" aria-label="開啟舊版 EasyLottery">
        舊版系統
      </v-btn>

      <v-btn
        v-if="auth.isAuthenticated"
        class="sign-out-button"
        icon="mdi-logout"
        variant="text"
        aria-label="管理員登出"
        @click="signOut"
      />
    </v-app-bar>

    <v-navigation-drawer class="desktop-drawer d-none d-md-flex" permanent width="264">
      <div class="drawer-caption">EASYLOTTERY WORKSPACE</div>
      <v-list nav aria-label="主要導覽">
        <v-list-item
          v-for="item in navigationItems"
          :key="item.title"
          :prepend-icon="item.icon"
          :title="item.title"
          :to="item.to"
          rounded="lg"
        />
      </v-list>
      <template #append>
        <div class="drawer-legacy-link">
          <v-btn block variant="tonal" prepend-icon="mdi-arrow-top-right" href="/">
            前往完整舊版
          </v-btn>
        </div>
      </template>
    </v-navigation-drawer>

    <v-navigation-drawer
      v-model="mobileDrawerOpen"
      class="mobile-drawer d-md-none"
      temporary
      width="288"
    >
      <div class="drawer-caption">EASYLOTTERY WORKSPACE</div>
      <v-list nav aria-label="主要導覽">
        <v-list-item
          v-for="item in navigationItems"
          :key="item.title"
          :prepend-icon="item.icon"
          :title="item.title"
          :to="item.to"
          rounded="lg"
          @click="mobileDrawerOpen = false"
        />
        <v-list-item
          prepend-icon="mdi-arrow-top-right"
          title="前往完整舊版"
          href="/"
          rounded="lg"
        />
      </v-list>
    </v-navigation-drawer>

    <v-main>
      <RouterView />
    </v-main>

    <v-footer class="app-footer" color="background">
      <div class="footer-content">
        <span>EasyLottery · 直播互動與抽獎管理</span>
        <RouterLink :to="{ name: 'privacy' }">隱私權政策</RouterLink>
      </div>
    </v-footer>
  </v-app>
</template>

<style scoped>
.app-toolbar {
  z-index: 5;
  border-bottom: 1px solid #e1eae5;
}

.brand-button {
  gap: 0.7rem;
  min-width: auto;
  padding-inline: 0.5rem;
  color: #173d33;
}

.brand-mark {
  color: white;
  font-size: 0.78rem;
  font-weight: 700;
  letter-spacing: 0.04em;
}

.brand-name {
  font-size: 1.15rem;
  font-weight: 700;
  letter-spacing: -0.02em;
}

.toolbar-links {
  align-items: center;
  margin-right: 0.65rem;
}

.legacy-link {
  border-color: #d1dfd8;
}

.sign-out-button {
  margin-left: 0.25rem;
}

.desktop-drawer,
.mobile-drawer {
  border-right: 1px solid #e1eae5;
  background: #fbfdfb;
}

.drawer-caption {
  padding: 1.5rem 1.25rem 0.75rem;
  color: #6a8178;
  font-size: 0.7rem;
  font-weight: 700;
  letter-spacing: 0.12em;
}

.drawer-legacy-link {
  padding: 1rem;
}

.app-footer {
  border-top: 1px solid #e1eae5;
}

.footer-content {
  display: flex;
  width: min(100%, 1160px);
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  margin-inline: auto;
  padding-inline: clamp(1rem, 3vw, 2rem);
}

.footer-content a {
  color: #145c4b;
  font-weight: 600;
  text-decoration: none;
}

.footer-content a:hover {
  text-decoration: underline;
}

@media (max-width: 600px) {
  .brand-name {
    font-size: 1rem;
  }

  .legacy-link {
    min-width: 0;
    padding-inline: 0.65rem;
    font-size: 0.8rem;
  }

  .footer-content {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
