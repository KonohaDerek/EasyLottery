import { describe, expect, it } from "vitest";
import { isValidEmail } from "./email";

describe("isValidEmail", () => {
  it("accepts a normal address and trims surrounding whitespace", () => {
    expect(isValidEmail(" admin@example.com ")).toBe(true);
  });

  it("rejects blank and malformed values", () => {
    expect(isValidEmail("")).toBe(false);
    expect(isValidEmail("not-an-email")).toBe(false);
    expect(isValidEmail("admin@")).toBe(false);
  });
});
