import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The production build is emitted into the host's wwwroot so the dashboard is
// served from the same origin as the API and SignalR hub (no cross-site cookies).
const backend = process.env.NEBULOG_BACKEND ?? 'http://localhost:5080'

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../../src/NebuLog.Server.Host/wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/api': backend,
      '/health': backend,
      '/hubs': { target: backend, ws: true },
    },
  },
})
