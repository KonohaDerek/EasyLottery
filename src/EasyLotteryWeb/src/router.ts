import { createRouter, createWebHistory } from "vue-router";
import { routes, publicRoutes } from "./routes";
import { adminRouteRedirect, shopRouteRedirect } from "./router-guard";
import { getStoredToken, isAdminSessionToken } from "./stores/session";

export { adminRoutes, publicRoutes, routes } from "./routes";

export const router = createRouter({
  history: createWebHistory("/"),
  routes
});

router.beforeEach(async to => {
  const requestedPath = to.path === "/pokebox" && typeof to.query.edit === "string"
    ? `${to.path}?edit=${encodeURIComponent(to.query.edit)}`
    : to.path;
  const redirect = to.meta.requiresAuth ? adminRouteRedirect(requestedPath, isAdminSessionToken(getStoredToken())) : null;
  if (redirect) return redirect;
  if (to.meta.requiresShop === true) {
    const { loadShopFeatureStatus } = await import("./card-hub/api");
    const status = await loadShopFeatureStatus().catch(() => ({ enabled: false }));
    const shopRedirect = shopRouteRedirect(status.enabled);
    if (shopRedirect) return shopRedirect;
  }
  return true;
});

router.afterEach(to => {
  document.title = String(to.meta.title ?? "EasyLottery｜直播互動抽獎系統");
});
