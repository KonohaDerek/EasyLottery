import { createRouter, createWebHistory } from "vue-router";
import { routes, publicRoutes } from "./routes";
import { adminRouteRedirect } from "./router-guard";
import { getStoredToken, isAdminSessionToken } from "./stores/session";

export { adminRoutes, publicRoutes, routes } from "./routes";

export const router = createRouter({
  history: createWebHistory("/"),
  routes
});

router.beforeEach(to => {
  const redirect = to.meta.requiresAuth ? adminRouteRedirect(to.path, isAdminSessionToken(getStoredToken())) : null;
  if (redirect) return redirect;
  return true;
});

router.afterEach(to => {
  document.title = String(to.meta.title ?? "EasyLottery｜直播互動抽獎系統");
});
