import { describe, expect, it } from "vitest";
import { publicRoutes } from "./routes";

describe("public Vue routes", () => {
  it("keeps the Phase 1 routes explicit and public", () => {
    expect(publicRoutes.map(route => route.path)).toEqual(["/about", "/privacy-policy", "/login"]);
    expect(publicRoutes.every(route => !route.meta?.requiresAdmin)).toBe(true);
  });
});
