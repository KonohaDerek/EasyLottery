import { createRouter, createWebHistory } from "vue-router";
import { publicRoutes } from "./routes";

export { publicRoutes } from "./routes";

export const router = createRouter({
  history: createWebHistory("/"),
  routes: publicRoutes
});

router.afterEach(to => {
  document.title = String(to.meta.title ?? "EasyLottery｜直播互動抽獎系統");
});
