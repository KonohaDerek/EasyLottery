import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

export default defineConfig(({ command }) => ({
  base: command === "build" ? "/vue/" : "/",
  plugins: [vue()],
  server: {
    proxy: {
      "/api": "http://localhost:18930",
      "/settings": "http://localhost:18930",
      "/easy-lottery-config.yaml": "http://localhost:18930"
    }
  },
  build: {
    emptyOutDir: true,
    outDir: "../EasyLotteryWasm/wwwroot/vue"
  },
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"]
  }
}));
