import { describe, expect, it } from "vitest";
import { adminRoutes, publicRoutes } from "./routes";
import { adminRouteRedirect } from "./router-guard";

describe("public Vue routes", () => {
  it("keeps the Phase 1 routes explicit and public", () => {
    expect(publicRoutes.map(route => route.path)).toEqual(["/about", "/privacy-policy", "/login"]);
    expect(publicRoutes.every(route => !route.meta?.requiresAdmin)).toBe(true);
  });
});

describe("admin route guard", () => {
  it("registers Donate activities as an authenticated route", () => {
    expect(adminRoutes.find(route => route.path === "/donate-activities")?.meta?.requiresAuth).toBe(true);
  });

  it("registers activity results as an authenticated route", () => {
    expect(adminRoutes.find(route => route.path === "/activity-results")?.meta?.requiresAuth).toBe(true);
    expect(adminRoutes.find(route => route.path === "/roulette")?.meta?.requiresAuth).toBe(true);
    expect(adminRoutes.find(route => route.path === "/pokebox")?.meta?.requiresAuth).toBe(true);
    expect(adminRoutes.find(route => route.path === "/pokebox/preview/:id")?.meta?.requiresAuth).toBe(true);
    expect(adminRoutes.find(route => route.path === "/roulette/preview/:id")?.meta?.requiresAuth).toBe(true);
  });

  it("redirects unauthenticated admin navigation to login", () => {
    expect(adminRouteRedirect("/donate-activities", false)).toBe("/login?redirect=%2Fdonate-activities");
    expect(adminRouteRedirect("/system/payment", false)).toBe("/login?redirect=%2Fsystem%2Fpayment");
    expect(adminRouteRedirect("/activity-results", false)).toBe("/login?redirect=%2Factivity-results");
    expect(adminRouteRedirect("/roulette", false)).toBe("/login?redirect=%2Froulette");
    expect(adminRouteRedirect("/pokebox?edit=7", false)).toBe("/login?redirect=%2Fpokebox%3Fedit%3D7");
    expect(adminRouteRedirect("/pokebox/preview/7", false)).toBe("/login?redirect=%2Fpokebox%2Fpreview%2F7");
    expect(adminRouteRedirect("/roulette/preview/7", false)).toBe("/login?redirect=%2Froulette%2Fpreview%2F7");
  });

  it("allows an authenticated session to continue", () => {
    expect(adminRouteRedirect("/system/payment", true)).toBeNull();
  });
});
