import { defineConfig, externalizeDepsPlugin } from 'electron-vite'

const adminUiDevUrl =
  process.env.ADMIN_UI_URL ||
  process.env.services__adminui__https__0 ||
  process.env.services__adminui__http__0 ||
  'http://localhost:5173'

export default defineConfig({
  main: {
    plugins: [externalizeDepsPlugin()],
    build: {
      rollupOptions: {
        input: 'src/main/index.ts'
      }
    },
    define: {
      ADMIN_UI_DEV_URL: JSON.stringify(adminUiDevUrl)
    }
  },
  preload: {
    plugins: [externalizeDepsPlugin()],
    build: {
      rollupOptions: {
        input: 'src/preload/index.ts'
      }
    }
  },
  renderer: {
    // The renderer is the AdminUI web app.
    // In dev: we load the AdminUI Vite dev server directly (no renderer build).
    // In prod: the prebuild script copies AdminUI's dist/ into out/renderer/.
    root: '.',
    build: {
      rollupOptions: {
        input: 'src/renderer/index.html'
      }
    }
  }
})
