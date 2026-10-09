import { describe, expect, it } from "vitest";
import { safePostLoginRedirect } from "./redirect";

describe("safePostLoginRedirect", () => {
  const origin = "https://lottery.example";

  it("preserves a same-origin path, query, and hash", () => {
    expect(safePostLoginRedirect("/system/payment?tab=ecpay#testing", origin))
      .toBe("/system/payment?tab=ecpay#testing");
  });

  it.each([
    "https://evil.example/path",
    `${origin}//evil.example/path`,
    "http://["
  ])("uses the admin route for unsafe redirect %s", redirect => {
    expect(safePostLoginRedirect(redirect, origin)).toBe("/system/access");
  });
});
