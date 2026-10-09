import type { RouteRecordRaw } from "vue-router";
import AboutView from "./views/AboutView.vue";
import LoginView from "./views/LoginView.vue";
import PrivacyPolicyView from "./views/PrivacyPolicyView.vue";
import AdminAccessView from "./views/AdminAccessView.vue";
import AuditView from "./views/AuditView.vue";
import BackupsView from "./views/BackupsView.vue";
import ObsAssetsView from "./views/ObsAssetsView.vue";
import ObsLayoutsView from "./views/ObsLayoutsView.vue";
import PaymentSettingsView from "./views/PaymentSettingsView.vue";
import SoundCuesView from "./views/SoundCuesView.vue";
import VisualStylesView from "./views/VisualStylesView.vue";
import YoutubeLoginView from "./views/YoutubeLoginView.vue";

export const publicRoutes: RouteRecordRaw[] = [
  { path: "/about", component: AboutView, meta: { title: "關於 EasyLottery" } },
  { path: "/privacy-policy", component: PrivacyPolicyView, meta: { title: "隱私權政策｜EasyLottery" } },
  { path: "/login", component: LoginView, meta: { title: "管理員登入｜EasyLottery" } }
];

export const adminRoutes: RouteRecordRaw[] = [
  { path: "/system/access", component: AdminAccessView, meta: { title: "管理員 Passkey 管理｜EasyLottery", requiresAuth: true } },
  { path: "/system/payment", component: PaymentSettingsView, meta: { title: "支付配置｜EasyLottery", requiresAuth: true } },
  { path: "/system/youtube-login", component: YoutubeLoginView, meta: { title: "YouTube API Key｜EasyLottery", requiresAuth: true } },
  { path: "/system/audit", component: AuditView, meta: { title: "審計紀錄｜EasyLottery", requiresAuth: true } },
  { path: "/system/backups", component: BackupsView, meta: { title: "設定備份與回復｜EasyLottery", requiresAuth: true } },
  { path: "/system/obs-layouts", component: ObsLayoutsView, meta: { title: "OBS 版面｜EasyLottery", requiresAuth: true } },
  { path: "/system/obs-assets", component: ObsAssetsView, meta: { title: "OBS 資產｜EasyLottery", requiresAuth: true } },
  { path: "/system/sound-cues", component: SoundCuesView, meta: { title: "音效預設｜EasyLottery", requiresAuth: true } },
  { path: "/system/visual-styles", component: VisualStylesView, meta: { title: "視覺樣式｜EasyLottery", requiresAuth: true } }
];

export const routes: RouteRecordRaw[] = [...publicRoutes, ...adminRoutes];
