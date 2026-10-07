/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The shop is served by its own ASP.NET host, which the NebuLog host reverse-proxies under
// /apps/shop. Assets therefore have to be requested relative to the page, not from the site root.
const orders = process.env.NEBUSHOP_BACKEND ?? 'http://localhost:5081'

export default defineConfig(({ command }) => ({
  // Absolute, not './'. The shop is opened as /apps/shop?scenario=... with no trailing slash, and a
  // relative base resolves against /apps/ there — the assets 404 and the page renders blank. An
  // absolute base is correct whether or not the URL ends in a slash. Dev serves from the root.
  base: command === 'build' ? '/apps/shop/' : '/',
  plugins: [react()],
  build: {
    outDir: '../../samples/NebuShop.Orders/wwwroot',
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    globals: true,
    include: ['src/**/*.test.{ts,tsx}'],
  },
  server: {
    proxy: {
      '/api': orders,
    },
  },
}))
