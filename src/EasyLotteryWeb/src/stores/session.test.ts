import { describe, expect, it } from "vitest";
import { isAdminSessionToken } from "./session";

function token(claims: Record<string, unknown>): string {
  const payload = btoa(JSON.stringify(claims)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
  return `header.${payload}.signature`;
}

describe("admin session token", () => {
  it("accepts an unexpired admin token", () => {
    expect(isAdminSessionToken(token({ token_use: "admin", exp: 2_000_000_000 }), 1_900_000_000_000)).toBe(true);
  });

  it("rejects OBS, expired, and malformed tokens", () => {
    expect(isAdminSessionToken(token({ token_use: "obs", exp: 2_000_000_000 }), 1_900_000_000_000)).toBe(false);
    expect(isAdminSessionToken(token({ token_use: "admin", exp: 1_800_000_000 }), 1_900_000_000_000)).toBe(false);
    expect(isAdminSessionToken("not-a-jwt", 1_900_000_000_000)).toBe(false);
  });
});
