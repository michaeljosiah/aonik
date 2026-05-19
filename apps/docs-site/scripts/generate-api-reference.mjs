// Generates content/docs/api/reference/**/*.mdx from openapi/aonik-api.yaml
// using fumadocs-openapi. The generated MDX files reference the spec by
// absolute filesystem path, so this script is run at build time
// (`npm run prebuild`) rather than committed.
//
// Run manually with `npm run gen:api`.

import { generateFiles } from 'fumadocs-openapi';
import { createOpenAPI } from 'fumadocs-openapi/server';
import { readdir, rm } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve, join } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = resolve(__dirname, '..');
const specPath = resolve(root, 'openapi', 'aonik-api.yaml');
const outputDir = resolve(root, 'content', 'docs', 'api', 'reference');

if (!existsSync(specPath)) {
  console.error(`[gen:api] OpenAPI spec not found at ${specPath}`);
  process.exit(1);
}

// Wipe everything inside outputDir EXCEPT the hand-written index.mdx.
// We need to clean tag folders so removed endpoints are pruned, but we keep
// the operator-authored landing page.
if (existsSync(outputDir)) {
  const entries = await readdir(outputDir, { withFileTypes: true });
  await Promise.all(
    entries
      .filter((entry) => entry.name !== 'index.mdx')
      .map((entry) =>
        rm(join(outputDir, entry.name), { recursive: true, force: true }),
      ),
  );
}

const openapi = createOpenAPI({
  input: [specPath],
});

await generateFiles({
  input: openapi,
  output: outputDir,
  // Group endpoints by tag — one folder per tag, one page per operation.
  groupBy: 'tag',
  // Emit a meta.json per folder so the sidebar order is deterministic.
  meta: true,
});

console.log(`[gen:api] Generated API reference at ${outputDir}`);
