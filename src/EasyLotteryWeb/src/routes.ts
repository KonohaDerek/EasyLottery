import type { RouteRecordRaw } from "vue-router";
import AboutView from "./views/AboutView.vue";
import LoginView from "./views/LoginView.vue";
import LotteryHomeView from "./views/LotteryHomeView.vue";
import PrivacyPolicyView from "./views/PrivacyPolicyView.vue";
import AdminAccessView from "./views/AdminAccessView.vue";
import ActivityResultsView from "./views/ActivityResultsView.vue";
import DonateActivitiesView from "./views/DonateActivitiesView.vue";
import RouletteView from "./views/RouletteView.vue";
import PokeBoxView from "./views/PokeBoxView.vue";
import PokeBoxPreviewView from "./views/PokeBoxPreviewView.vue";
import RoulettePreviewView from "./views/RoulettePreviewView.vue";
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
  { path: "/", component: LotteryHomeView, meta: { title: "抽獎工作區｜EasyLottery", requiresAuth: true } },
  { path: "/donate-activities", component: DonateActivitiesView, meta: { title: "Donate 活動｜EasyLottery", requiresAuth: true } },
  { path: "/activity-results", component: ActivityResultsView, meta: { title: "活動結果｜EasyLottery", requiresAuth: true } },
  { path: "/roulette", component: RouletteView, meta: { title: "轉盤模板｜EasyLottery", requiresAuth: true } },
  { path: "/roulette/preview/:id", component: RoulettePreviewView, meta: { title: "轉盤 OBS 預覽｜EasyLottery", requiresAuth: true } },
  { path: "/pokebox", component: PokeBoxView, meta: { title: "戳戳樂模板｜EasyLottery", requiresAuth: true } },
  { path: "/pokebox/preview/:id", component: PokeBoxPreviewView, meta: { title: "戳戳樂 OBS 預覽｜EasyLottery", requiresAuth: true } },
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

export const obsRoutes: RouteRecordRaw[] = [
  { path: "/obs/roulette/:publicId", component: () => import("./views/RouletteObsView.vue"), meta: { title: "轉盤 - OBS", obs: true } },
  { path: "/obs/pokebox/:publicId", component: () => import("./views/PokeBoxObsView.vue"), meta: { title: "戳戳樂 - OBS", obs: true } },
  { path: "/obs/overtime", component: () => import("./views/OvertimeObsView.vue"), meta: { title: "加班台 - OBS", obs: true } },
  { path: "/obs/donate/:activityRouteKey", component: () => import("./views/DonateObsView.vue"), meta: { title: "Donate 抽獎 - OBS", obs: true } }
];

export const routes: RouteRecordRaw[] = [...publicRoutes, ...adminRoutes, ...obsRoutes];
