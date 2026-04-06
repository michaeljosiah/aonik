import { defineConfig } from 'tsup';

export default defineConfig({
  entry: {
    'core/index': 'src/core/index.ts',
    'host/index': 'src/host/index.ts',
    'client/index': 'src/client/index.ts',
    'react/index': 'src/react/index.ts',
  },
  format: ['esm', 'cjs'],
  dts: true,
  splitting: true,
  treeshake: true,
  clean: true,
  external: ['react'],
  outDir: 'dist',
  minify: false,
});
