import { describe, expect, it } from "vitest";
import { adminRoutes, obsRoutes, publicRoutes } from "./routes";
import { adminRouteRedirect } from "./router-guard";

describe("public Vue routes", () => {
  it("keeps the Phase 1 routes explicit and public", () => {
    expect(publicRoutes.map(route => route.path)).toEqual(["/about", "/privacy-policy", "/login"]);
    expect(publicRoutes.every(route => !route.meta?.requiresAdmin)).toBe(true);
  });
});

describe("admin route guard", () => {
  it("registers the core lottery workspace as an authenticated root route", () => {
    expect(adminRoutes.find(route => route.path === "/")?.meta?.requiresAuth).toBe(true);
  });

  it("registers Donate activities as an authenticated route", () => {
    expect(adminRoutes.find(route => route.path === "/donate-activities")?.meta?.requiresAuth).toBe(true);
  });

  it("registers activity results as an authenticated route", () => {
    expect(adminRoutes.find(route => route.path === "/activity-results")?.meta?.requiresAuth).toBe(true);
    expect(adminRoutes.find(route => route.path === "/lottery-draw-records")?.meta?.requiresAuth).toBe(true);
    expect(adminRoutes.find(route => route.path === "/donate-lottery-records")?.meta?.requiresAuth).toBe(true);
    expect(adminRoutes.find(route => route.path === "/interactions/rounds")?.meta?.requiresAuth).toBe(true);
    expect(adminRoutes.find(route => route.path === "/interactions/identity-link")?.meta?.requiresAuth).toBe(true);
    expect(adminRoutes.find(route => route.path === "/roulette")?.meta?.requiresAuth).toBe(true);
    expect(adminRoutes.find(route => route.path === "/pokebox")?.meta?.requiresAuth).toBe(true);
    expect(adminRoutes.find(route => route.path === "/pokebox/preview/:id")?.meta?.requiresAuth).toBe(true);
    expect(adminRoutes.find(route => route.path === "/roulette/preview/:id")?.meta?.requiresAuth).toBe(true);
    expect(adminRoutes.find(route => route.path === "/market")?.meta?.requiresAuth).toBe(true);
    expect(adminRoutes.find(route => route.path === "/system/platform-connections")?.meta?.requiresAuth).toBe(true);
    expect(adminRoutes.find(route => route.path === "/activity/overtime")?.meta?.requiresAuth).toBe(true);
  });

  it("redirects unauthenticated admin navigation to login", () => {
    expect(adminRouteRedirect("/donate-activities", false)).toBe("/login?redirect=%2Fdonate-activities");
    expect(adminRouteRedirect("/system/payment", false)).toBe("/login?redirect=%2Fsystem%2Fpayment");
    expect(adminRouteRedirect("/activity-results", false)).toBe("/login?redirect=%2Factivity-results");
    expect(adminRouteRedirect("/lottery-draw-records", false)).toBe("/login?redirect=%2Flottery-draw-records");
    expect(adminRouteRedirect("/donate-lottery-records", false)).toBe("/login?redirect=%2Fdonate-lottery-records");
    expect(adminRouteRedirect("/interactions/rounds", false)).toBe("/login?redirect=%2Finteractions%2Frounds");
    expect(adminRouteRedirect("/interactions/identity-link", false)).toBe("/login?redirect=%2Finteractions%2Fidentity-link");
    expect(adminRouteRedirect("/roulette", false)).toBe("/login?redirect=%2Froulette");
    expect(adminRouteRedirect("/pokebox?edit=7", false)).toBe("/login?redirect=%2Fpokebox%3Fedit%3D7");
    expect(adminRouteRedirect("/pokebox/preview/7", false)).toBe("/login?redirect=%2Fpokebox%2Fpreview%2F7");
    expect(adminRouteRedirect("/market", false)).toBe("/login?redirect=%2Fmarket");
    expect(adminRouteRedirect("/system/platform-connections", false)).toBe("/login?redirect=%2Fsystem%2Fplatform-connections");
    expect(adminRouteRedirect("/activity/overtime", false)).toBe("/login?redirect=%2Factivity%2Fovertime");
    expect(adminRouteRedirect("/roulette/preview/7", false)).toBe("/login?redirect=%2Froulette%2Fpreview%2F7");
  });

  it("allows an authenticated session to continue", () => {
    expect(adminRouteRedirect("/system/payment", true)).toBeNull();
  });
});

describe("OBS overlay routes", () => {
  it("registers the overlays without the admin guard or public shell", () => {
    expect(obsRoutes).toHaveLength(8);
    expect(obsRoutes.map(route => route.path)).toEqual([
      "/obs/roulette/:publicId",
      "/obs/pokebox/:publicId",
      "/obs/overtime",
      "/obs/donate/:activityRouteKey",
      "/obs/interaction-vote/:roundId",
      "/obs/interaction-notice/:roundId",
      "/obs/interaction-status/:roundId",
      "/obs/interaction-leaderboard/:roundId"
    ]);
    expect(obsRoutes.every(route => route.meta?.obs === true && route.meta?.requiresAuth !== true)).toBe(true);
  });
});
