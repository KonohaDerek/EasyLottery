import { describe, expect, it } from "vitest";
import { publicRoutes } from "./routes";
import { adminRouteRedirect } from "./router-guard";

describe("public Vue routes", () => {
  it("keeps the Phase 1 routes explicit and public", () => {
    expect(publicRoutes.map(route => route.path)).toEqual(["/about", "/privacy-policy", "/login"]);
    expect(publicRoutes.every(route => !route.meta?.requiresAdmin)).toBe(true);
  });
});

describe("admin route guard", () => {
  it("redirects unauthenticated admin navigation to login", () => {
    expect(adminRouteRedirect("/system/payment", false)).toBe("/login?redirect=%2Fsystem%2Fpayment");
  });

  it("allows an authenticated session to continue", () => {
    expect(adminRouteRedirect("/system/payment", true)).toBeNull();
  });
});
