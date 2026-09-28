import path from 'path';

import { defineConfig } from 'vitest/config';

export default defineConfig({
  resolve: {
    preserveSymlinks: true,
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  test: {
    environment: 'node',
    server: { deps: { inline: ['@aonik/workspace-sdk'] } },
  },
});
