// Design-system guardrails for the Admin UI (Spec 098 §11).
//
// Scans src/**/*.tsx outside components/ui (the primitive layer) and
// pages/dev, and counts patterns the shadcn/Radix alignment removes:
// hard-coded colours, raw Tailwind palette colours, arbitrary radii and
// z-indexes, window.confirm, raw form/table elements, and legacy CSS
// classes. Prints a per-folder table.
//
//   npm run check:design            report only (exit 0)
//   npm run check:design -- --strict   exit 1 if any rule has matches
//   npm run check:design -- --files    also list each match
//   npm run check:design -- --only pages/orders   limit to a folder
//
// A justified exception is marked `// guardrail-ignore: <reason>` on the
// line itself or the line above (JSX: {/* guardrail-ignore: <reason> */}).

import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const srcDir = fileURLToPath(new URL('../src', import.meta.url));
const args = process.argv.slice(2);
const strict = args.includes('--strict');
const listFiles = args.includes('--files');
const onlyIdx = args.indexOf('--only');
const only = onlyIdx >= 0 ? args[onlyIdx + 1]?.replace(/\\/g, '/') : null;

const EXCLUDE = ['components/ui/', 'pages/dev/'];

const PALETTE =
  '(?:gray|slate|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose)';

/** Each rule: id, description, regex (global), optional line filter. */
const RULES = [
  {
    id: 'hex',
    label: 'Hex colour literals',
    re: /(?<![\w&])#[0-9a-fA-F]{3}(?:[0-9a-fA-F]{3}(?:[0-9a-fA-F]{2})?)?\b/g,
    // Skip URL fragments, JSX entities and obvious non-colour usages.
    skipLine: (line) => /^\s*(\/\/|\*|\/\*)/.test(line) || /href=|to=|#\/|&#/.test(line),
  },
  {
    id: 'palette',
    label: 'Raw Tailwind palette colours',
    re: new RegExp(
      `\\b(?:bg|text|border|ring|fill|stroke|from|to|via|divide|outline|decoration|placeholder|shadow|accent|caret)-${PALETTE}-\\d{2,3}\\b`,
      'g',
    ),
  },
  { id: 'radius', label: 'Arbitrary / zero radius', re: /\brounded(?:-[trblse]{1,2})?-(?:\[[^\]]+\]|none)(?![\w-])/g },
  { id: 'zindex', label: 'Arbitrary z-index', re: /\bz-\[\d+\]/g },
  { id: 'confirm', label: 'window.confirm', re: /\bwindow\.confirm\s*\(/g },
  { id: 'select', label: 'Raw <select>', re: /<select[\s>]/g },
  { id: 'table', label: 'Raw <table>', re: /<table[\s>]/g },
  { id: 'checkbox', label: 'Native checkbox/radio', re: /type=["'](?:checkbox|radio)["']/g },
  {
    id: 'legacyClass',
    label: 'Legacy CSS classes',
    re: /(?<=["'`\s])(?:hover-halo|eyebrow|aonik-input|aonik-select|theme-(?:text-color|bg|bg-light|border|active)|hover-theme-effect|theme-image-hover|theme-text-hover|hoverBorder|hover-border|shine-effect|input-focused|zoom-responsive)(?=["'`\s])/g,
  },
  { id: 'legacyVar', label: 'Legacy var(--color-*) (info, P4)', re: /var\(--color-[a-z0-9-]+\)/g, info: true },
];

function walk(dir, out = []) {
  for (const name of readdirSync(dir)) {
    const p = join(dir, name);
    if (statSync(p).isDirectory()) walk(p, out);
    else if (p.endsWith('.tsx') && !p.endsWith('.test.tsx')) out.push(p);
  }
  return out;
}

function folderOf(rel) {
  const parts = rel.split('/');
  if (parts[0] === 'pages' || parts[0] === 'components' || parts[0] === 'workspace' || parts[0] === 'modules') {
    return parts.length > 2 ? `${parts[0]}/${parts[1]}` : parts[0];
  }
  return parts.length > 1 ? parts[0] : '(root)';
}

const files = walk(srcDir)
  .map((p) => ({ p, rel: relative(srcDir, p).split(sep).join('/') }))
  .filter(({ rel }) => !EXCLUDE.some((e) => rel.startsWith(e)))
  .filter(({ rel }) => !only || rel.startsWith(only));

const totals = Object.fromEntries(RULES.map((r) => [r.id, 0]));
const byFolder = new Map();
const matches = [];

for (const { p, rel } of files) {
  const lines = readFileSync(p, 'utf8').split(/\r?\n/);
  const folder = folderOf(rel);
  if (!byFolder.has(folder)) byFolder.set(folder, Object.fromEntries(RULES.map((r) => [r.id, 0])));
  const counts = byFolder.get(folder);
  lines.forEach((line, i) => {
    // A deliberate exception carries a reason: `// guardrail-ignore: <why>`
    // on the same line or the line above.
    if (/guardrail-ignore/.test(line) || /guardrail-ignore/.test(lines[i - 1] ?? '')) return;
    for (const rule of RULES) {
      if (rule.skipLine?.(line)) continue;
      const found = line.match(rule.re);
      if (!found) continue;
      counts[rule.id] += found.length;
      totals[rule.id] += found.length;
      if (listFiles && !rule.info) matches.push(`${rel}:${i + 1}  [${rule.id}] ${found.join(', ')}`);
    }
  });
}

const ids = RULES.map((r) => r.id);
const pad = (s, n) => String(s).padEnd(n);
const rows = [...byFolder.entries()]
  .filter(([, c]) => ids.some((id) => c[id] > 0))
  .sort(([a], [b]) => a.localeCompare(b));

console.log(pad('folder', 30) + ids.map((id) => pad(id, 12)).join(''));
for (const [folder, c] of rows) console.log(pad(folder, 30) + ids.map((id) => pad(c[id] || '', 12)).join(''));
console.log(pad('TOTAL', 30) + ids.map((id) => pad(totals[id], 12)).join(''));
console.log('\n' + RULES.map((r) => `${r.id}: ${r.label}`).join('\n'));

if (listFiles) console.log('\n' + matches.join('\n'));

const blocking = RULES.filter((r) => !r.info).reduce((n, r) => n + totals[r.id], 0);
if (strict && blocking > 0) {
  console.error(`\n${blocking} guardrail match(es).`);
  process.exit(1);
}
