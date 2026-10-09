import type { RouteRecordRaw } from "vue-router";
import AboutView from "./views/AboutView.vue";
import LoginView from "./views/LoginView.vue";
import PrivacyPolicyView from "./views/PrivacyPolicyView.vue";

export const publicRoutes: RouteRecordRaw[] = [
  { path: "/about", component: AboutView, meta: { title: "關於 EasyLottery" } },
  { path: "/privacy-policy", component: PrivacyPolicyView, meta: { title: "隱私權政策｜EasyLottery" } },
  { path: "/login", component: LoginView, meta: { title: "管理員登入｜EasyLottery" } }
];
