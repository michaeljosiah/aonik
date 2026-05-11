import { defineConfig, externalizeDepsPlugin } from 'electron-vite'

const adminUiDevUrl =
  process.env.ADMIN_UI_URL ||
  process.env.services__adminui__https__0 ||
  process.env.services__adminui__http__0 ||
  'http://localhost:5173'

// Baked-in API base URL the renderer falls back to when no runtime override
// (AONIK_API_URL / Aspire service discovery) is present. Defaults to the
// deployed dev environment so signed Windows installers work out of the box.
const apiDefaultUrl =
  process.env.AONIK_API_DEFAULT_URL ||
  'https://aonik-dev-api.delightfulisland-9fd7c1e7.uksouth.azurecontainerapps.io'

export default defineConfig({
  main: {
    plugins: [externalizeDepsPlugin()],
    build: {
      rollupOptions: {
        input: 'src/main/index.ts'
      }
    },
    define: {
      ADMIN_UI_DEV_URL: JSON.stringify(adminUiDevUrl),
      AONIK_API_DEFAULT_URL: JSON.stringify(apiDefaultUrl)
    }
  },
  preload: {
    plugins: [externalizeDepsPlugin()],
    build: {
      rollupOptions: {
        input: 'src/preload/index.ts',
        // Sandboxed preload scripts must be CommonJS — ESM `import` syntax
        // throws SyntaxError inside the renderer sandbox.
        output: {
          format: 'cjs',
          entryFileNames: '[name].js'
        }
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
