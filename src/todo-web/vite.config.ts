import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// The production bundle is written straight into the API's wwwroot, so one App Service
// serves both the SPA and /api. In dev, Vite proxies /api to the locally running API.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../TodoApp.Api/wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5280',
        changeOrigin: true,
      },
    },
  },
})
