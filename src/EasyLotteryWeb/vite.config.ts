import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

export default defineConfig({
  base: "/vue/",
  plugins: [vue()],
  build: {
    emptyOutDir: true,
    outDir: "../EasyLotteryWasm/wwwroot/vue"
  },
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"]
  }
});
