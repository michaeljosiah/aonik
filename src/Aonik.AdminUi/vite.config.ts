import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
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
        target: process.env.services__api__https__0 || process.env.services__api__http__0 || 'http://localhost:5049',
        changeOrigin: true,
        secure: false,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
      // Proxy static content media files served by the API
      '/storage': {
        target: process.env.services__api__https__0 || process.env.services__api__http__0 || 'http://localhost:5049',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
