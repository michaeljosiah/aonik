// Checks WCAG contrast for the design tokens in src/index.css (Spec 098 §6).
// Reads the raw hex tokens from the `:root` block and the first
// `[data-theme="dark"]` block, then checks each foreground/background pair:
// text pairs need 4.5:1, non-text pairs (focus ring, filled controls against
// the page) need 3:1. Exits non-zero on any failure.
//
//   npm run check:contrast

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const cssPath = fileURLToPath(new URL('../src/index.css', import.meta.url));
const css = readFileSync(cssPath, 'utf8');

function readBlock(selector) {
  const start = css.indexOf(`${selector} {`);
  if (start < 0) throw new Error(`Block ${selector} not found in index.css`);
  const body = css.slice(start, css.indexOf('}', start));
  const tokens = {};
  for (const [, name, value] of body.matchAll(/--([a-z0-9-]+):\s*(#[0-9a-fA-F]{6})\s*;/g)) {
    tokens[name] = value.toLowerCase();
  }
  return tokens;
}

const light = readBlock(':root');
const dark = { ...light, ...readBlock('[data-theme="dark"]') };

function luminance(hex) {
  const [r, g, b] = [1, 3, 5]
    .map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)
    .map((c) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function ratio(a, b) {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

const TEXT = 4.5;
const NON_TEXT = 3;

// [foreground, background, minimum]
const pairs = [
  ['foreground', 'background', TEXT],
  ['card-foreground', 'card', TEXT],
  ['popover-foreground', 'popover', TEXT],
  ['muted-foreground', 'background', TEXT],
  ['muted-foreground', 'card', TEXT],
  ['muted-foreground', 'muted', TEXT],
  ['primary-foreground', 'primary', TEXT],
  ['secondary-foreground', 'secondary', TEXT],
  ['accent-foreground', 'accent', TEXT],
  ['destructive-foreground', 'destructive', TEXT],
  ['agent-foreground', 'agent', NON_TEXT],
  ['success-foreground', 'success-subtle', TEXT],
  ['warning-foreground', 'warning-subtle', TEXT],
  ['info-foreground', 'info-subtle', TEXT],
  ['sidebar-foreground', 'sidebar', TEXT],
  ['sidebar-accent-foreground', 'sidebar-accent', TEXT],
  ['sidebar-primary-foreground', 'sidebar-primary', TEXT],
  ['primary', 'background', NON_TEXT],
  ['primary', 'card', NON_TEXT],
  ['ring', 'background', NON_TEXT],
  ['ring', 'card', NON_TEXT],
  ['destructive', 'background', NON_TEXT],
  ['success', 'background', NON_TEXT],
  ['warning', 'background', NON_TEXT],
  ['info', 'background', NON_TEXT],
];

// Legacy call sites use the solids as text (text-[var(--color-success)],
// text-[var(--color-brand-primary)] and friends), so they must pass as text
// on the page and on cards in both themes.
const solidsAsText = ['primary', 'destructive', 'success', 'warning', 'info'].flatMap((t) => [
  [t, 'background', TEXT],
  [t, 'card', TEXT],
]);

let failures = 0;
function check(theme, tokens, [fg, bg, min]) {
  if (!tokens[fg] || !tokens[bg]) {
    console.error(`  ✗ ${theme}: missing token --${!tokens[fg] ? fg : bg}`);
    failures++;
    return;
  }
  const r = ratio(tokens[fg], tokens[bg]);
  const ok = r >= min;
  if (!ok) failures++;
  const line = `  ${ok ? '✓' : '✗'} ${theme.padEnd(5)} ${fg} on ${bg}: ${r.toFixed(2)}:1 (min ${min})`;
  (ok ? console.log : console.error)(line);
}

for (const pair of [...pairs, ...solidsAsText]) check('light', light, pair);
for (const pair of [...pairs, ...solidsAsText]) check('dark', dark, pair);

if (failures > 0) {
  console.error(`\n${failures} contrast pair(s) below minimum.`);
  process.exit(1);
}
console.log('\nAll contrast pairs pass.');
