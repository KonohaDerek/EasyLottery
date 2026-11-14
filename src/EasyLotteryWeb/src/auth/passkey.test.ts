import { describe, expect, it } from "vitest";
import { passkeyInteractionError } from "./passkey";

describe("Passkey browser errors", () => {
  for (const name of ["NotAllowedError", "AbortError"]) {
    it("uses the same actionable login message for " + name, () => {
      const cause = new DOMException("Browser-specific account detail", name);
      expect(passkeyInteractionError(cause, "login")).toMatchObject({
        message: "Passkey 登入已取消，或此裝置沒有可用的 Passkey。"
      });
    });
  }
  it("keeps registration cancellation distinct without account information", () => {
    expect(passkeyInteractionError(new DOMException("cancelled", "NotAllowedError"), "register"))
      .toMatchObject({ message: "Passkey 註冊已取消，請確認裝置是否支援 Passkey。" });
  });
  it("preserves unexpected errors for existing error handling", () => {
    const cause = new Error("network failed");
    expect(passkeyInteractionError(cause, "login")).toBe(cause);
  });
});
