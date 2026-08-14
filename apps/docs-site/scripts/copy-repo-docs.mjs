// Copies the repository's authored docs — the feature guides plus the folders
// they link into (specifications, ADRs, architecture notes, observability and
// runbook pages) — into public/, so the static export serves them verbatim and
// every relative link between them keeps resolving.
//
// Run at build time (`npm run prebuild`); public/ is generated, not committed.
// Run manually with `npm run gen:repo-docs`.

import { cp, mkdir, rm } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve, join } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = resolve(__dirname, '..');
const repoDocs = resolve(root, '..', '..', 'docs');
const publicDir = resolve(root, 'public');

// features is the deliverable; the rest are its (transitive) link targets.
// (A handful of specs also link ../architecture/, but no such folder exists in
// the repo — those links are dead at source, not something this copy can fix.)
const FOLDERS = [
  'features',
  'specifications',
  'decisions',
  'observability',
  'runbooks',
];

const missing = FOLDERS.filter((folder) => !existsSync(join(repoDocs, folder)));
if (missing.length > 0) {
  console.error(`[copy-docs] Missing under ${repoDocs}: ${missing.join(', ')}`);
  process.exit(1);
}

await mkdir(publicDir, { recursive: true });

for (const folder of FOLDERS) {
  const dest = join(publicDir, folder);
  // Wipe-and-recopy so files deleted from the repo are pruned from the site.
  await rm(dest, { recursive: true, force: true });
  await cp(join(repoDocs, folder), dest, { recursive: true });
  console.log(`[copy-docs] docs/${folder} -> public/${folder}`);
}
