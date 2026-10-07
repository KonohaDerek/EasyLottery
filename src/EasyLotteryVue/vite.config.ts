import { fileURLToPath, URL } from 'node:url'

import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'
import vuetify from 'vite-plugin-vuetify'

const apiTarget = process.env.EASYLOTTERY_API_PROXY ?? 'http://localhost:18930'

export default defineConfig({
  base: '/app/',
  plugins: [vue(), vuetify({ autoImport: true })],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': { target: apiTarget, changeOrigin: true },
      '/hubs': { target: apiTarget, changeOrigin: true, ws: true },
      '/settings': { target: apiTarget, changeOrigin: true },
    },
  },
  build: {
    outDir: '../EasyLotteryAPI/wwwroot/app',
    emptyOutDir: true,
  },
})
