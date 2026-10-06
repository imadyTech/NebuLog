import { defineConfig } from 'vite'

// Published inside the host's wwwroot so the page is same-origin with /v1/logs; that is what
// lets the sample work in production without any CORS configuration at all.
export default defineConfig({
  base: '/samples/browser/',
  build: {
    outDir: '../../src/NebuLog.Server.Host/wwwroot/samples/browser',
    emptyOutDir: true,
  },
  server: {
    proxy: { '/v1': process.env.NEBULOG_BACKEND ?? 'http://localhost:5080' },
  },
})
