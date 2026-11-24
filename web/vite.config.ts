import { fileURLToPath } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    // Accept any *.localhost workspace host so <tenant>.localhost:5173 works.
    allowedHosts: ['.localhost', 'localhost'],
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        // Preserve the incoming Host header. The API resolves the workspace from
        // the first host label, so rewriting it to localhost:5080 would strip the
        // tenant and reject sign-in with "Sign in at your workspace subdomain".
        changeOrigin: false,
      },
    },
  },
})
