import { createRouter, createWebHistory } from "vue-router";
import { routes, publicRoutes } from "./routes";
import { adminRouteRedirect } from "./router-guard";
import { getStoredToken, isAdminSessionToken } from "./stores/session";
import { shopRouteNotFoundRedirect } from "./shop/route-access";

export { adminRoutes, publicRoutes, routes } from "./routes";

export const router = createRouter({
  history: createWebHistory("/"),
  routes
});

router.beforeEach(async to => {
  const notFoundRedirect = await shopRouteNotFoundRedirect(
    to.path,
    to.name,
    to.query,
    to.hash
  );
  if (notFoundRedirect) return notFoundRedirect;

  const requestedPath = to.path === "/pokebox" && typeof to.query.edit === "string"
    ? `${to.path}?edit=${encodeURIComponent(to.query.edit)}`
    : to.path;
  const redirect = to.meta.requiresAuth ? adminRouteRedirect(requestedPath, isAdminSessionToken(getStoredToken())) : null;
  if (redirect) return redirect;
  return true;
});

router.afterEach(to => {
  document.title = String(to.meta.title ?? "EasyLottery｜直播互動抽獎系統");
});
