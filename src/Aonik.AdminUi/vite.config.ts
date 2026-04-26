import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'
import { readFileSync } from 'node:fs'
import { agentIconsManifest } from './plugins/agent-icons-manifest'

// Prefer the HTTPS endpoint to avoid HTTP→HTTPS redirects that strip the
// Authorization header (standard behaviour when following cross-origin 307s).
const apiTarget = process.env.services__api__https__0 || 'https://localhost:5001'

const pkg = JSON.parse(
  readFileSync(path.resolve(__dirname, './package.json'), 'utf-8')
) as { version: string }

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss(), agentIconsManifest()],
  define: {
    __APP_VERSION__: JSON.stringify(pkg.version),
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: parseInt(process.env.PORT || '5173'),
    strictPort: true,
    proxy: {
      // Proxy API requests to the backend during development
      '/api': {
        target: apiTarget,
        changeOrigin: true,
        secure: false,
        rewrite: (path) => path.replace(/^\/api/, ''),
        timeout: 120000, // 2 min socket timeout — LLM calls can take 15-30s
        proxyTimeout: 120000, // 2 min proxy timeout for target response
      },
      // Proxy static content media files served by the API
      '/storage': {
        target: apiTarget,
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
