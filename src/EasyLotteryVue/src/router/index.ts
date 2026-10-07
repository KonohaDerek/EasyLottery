import { createRouter, createWebHistory } from 'vue-router'

import ShellLayout from '@/layouts/ShellLayout.vue'
import AboutView from '@/views/AboutView.vue'
import HomeView from '@/views/HomeView.vue'
import LoginView from '@/views/LoginView.vue'
import NotFoundView from '@/views/NotFoundView.vue'
import PasskeyManagementView from '@/views/PasskeyManagementView.vue'
import PrivacyView from '@/views/PrivacyView.vue'
import { useAuthStore } from '@/stores/auth'
import { pinia } from '@/stores/pinia'

export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      component: ShellLayout,
      children: [
        { path: '', name: 'home', component: HomeView, meta: { title: '首頁' } },
        { path: 'about', name: 'about', component: AboutView, meta: { title: '關於 EasyLottery' } },
        { path: 'privacy-policy', name: 'privacy', component: PrivacyView, meta: { title: '隱私權政策' } },
        { path: 'login', name: 'login', component: LoginView, meta: { title: '管理員登入' } },
        {
          path: 'system/access',
          name: 'passkeys',
          component: PasskeyManagementView,
          meta: { title: 'Passkey 管理', requiresAuth: true },
        },
        { path: ':pathMatch(.*)*', name: 'not-found', component: NotFoundView, meta: { title: '找不到頁面' } },
      ],
    },
  ],
  scrollBehavior: () => ({ top: 0 }),
})

router.beforeEach((to) => {
  const auth = useAuthStore(pinia)
  auth.refreshSession()
  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
  return true
})

router.afterEach((to) => {
  const title = typeof to.meta.title === 'string' ? to.meta.title : '直播互動與抽獎管理'
  document.title = `${title}｜EasyLottery`
})
