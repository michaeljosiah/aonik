import fs from 'fs';
import path from 'path';
import type { Plugin } from 'vite';

const ICONS_DIR = 'public/images/agents';
const MANIFEST_FILE = path.join(ICONS_DIR, 'manifest.json');
const IMAGE_EXTENSIONS = new Set(['.png', '.jpg', '.jpeg', '.svg', '.webp']);

function buildManifest(rootDir: string): string[] {
  const dir = path.resolve(rootDir, ICONS_DIR);
  if (!fs.existsSync(dir)) return [];

  return fs
    .readdirSync(dir)
    .filter((file) => {
      if (file === 'manifest.json') return false;
      const ext = path.extname(file).toLowerCase();
      return IMAGE_EXTENSIONS.has(ext);
    })
    .sort()
    .map((file) => `/images/agents/${file}`);
}

function writeManifest(rootDir: string) {
  const icons = buildManifest(rootDir);
  const outPath = path.resolve(rootDir, MANIFEST_FILE);
  fs.writeFileSync(outPath, JSON.stringify(icons, null, 2) + '\n');
}

/**
 * Vite plugin that generates public/images/agents/manifest.json listing
 * every image file in the agents icon directory. Regenerates on file changes
 * in dev mode so new icons are picked up without recompilation.
 */
export function agentIconsManifest(): Plugin {
  let root = '';

  return {
    name: 'agent-icons-manifest',

    configResolved(config) {
      root = config.root;
      writeManifest(root);
    },

    configureServer(server) {
      const dir = path.resolve(root, ICONS_DIR);
      // Watch the icons directory for additions/removals
      server.watcher.add(dir);
      server.watcher.on('all', (event, filePath) => {
        if (!filePath.startsWith(dir)) return;
        if (path.basename(filePath) === 'manifest.json') return;
        if (event === 'add' || event === 'unlink') {
          writeManifest(root);
        }
      });
    },
  };
}
