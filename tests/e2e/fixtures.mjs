import { test as base, expect } from "@playwright/test";
import fs from "node:fs/promises";

export const test = base.extend({
  page: async ({ page }, use, testInfo) => {
    const entries = [];
    page.on("console", message => entries.push(`[console:${message.type()}] ${message.text()}`));
    page.on("pageerror", error => entries.push(`[pageerror] ${error.stack ?? error.message}`));
    page.on("requestfailed", request => entries.push(`[requestfailed] ${request.method()} ${request.url()} ${request.failure()?.errorText ?? "unknown"}`));
    await use(page);
    if (testInfo.status !== testInfo.expectedStatus && entries.length > 0) {
      await fs.writeFile(testInfo.outputPath("console.log"), `${entries.join("\n")}\n`, "utf8");
    }
  }
});

export { expect };

