import { defineConfig } from "@playwright/test";

// Read-only smoke/regression suite against a deployed site. Never point it at
// an environment where an extra rate-limited auth request would be a problem.
export default defineConfig({
  testDir: ".",
  testMatch: "**/*.spec.mjs",
  timeout: 60_000,
  expect: { timeout: 15_000 },
  workers: 1,
  fullyParallel: false,
  retries: 0,
  reporter: [["list"], ["html", { outputFolder: "../playwright-report-live", open: "never" }]],
  use: {
    baseURL: process.env.LIVE_BASE_URL ?? "https://easylotter.k-derek.synology.me",
    headless: true,
    trace: "retain-on-failure",
    screenshot: "only-on-failure"
  },
  outputDir: "../test-results-live"
});
