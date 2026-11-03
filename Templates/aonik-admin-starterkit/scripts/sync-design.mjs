import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import vm from 'node:vm';

const kit = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const repo = path.resolve(kit, '../..');
const read = (file) => readFile(path.join(repo, file), 'utf8');
const css = await read('src/Aonik.AdminUi/src/index.css');
const blocks = [':root', '[data-theme="dark"]'].map(selector => {
  const start = css.indexOf(`${selector} {`);
  if (start < 0) throw new Error(`Missing token block ${selector}`);
  return css.slice(start, css.indexOf('\n}', start) + 2);
});
const tokenOutput = `/* Generated from src/Aonik.AdminUi/src/index.css. Run node scripts/sync-design.mjs. */\n${blocks.join('\n\n')}\n`;

const profiles = {};
for (const id of ['general', 'food-commerce', 'arke-kids', 'remittance']) {
  profiles[id] = JSON.parse(await read(`src/Aonik.AdminUi/src/config/admin-profiles/${id}.json`));
}
const catalog = await read('src/Aonik.AdminUi/src/modules/profileCatalog.ts');
const businessTypes = vm.runInNewContext(`(${catalog.match(/adminProfileBusinessTypes[^=]*=\s*({[\s\S]*?});/)[1]})`);
const packs = {};
for (const id of ['base', 'simi', 'food-commerce', 'remittance']) {
  packs[id] = JSON.parse(await read(`src/Aonik.SharedKernel/Abstractions/Packs/Data/${id}.pack.json`));
}
const moduleIdsSource = await read('src/Aonik.SharedKernel/Modules/ModuleIds.cs');
const moduleIds = Object.fromEntries([...moduleIdsSource.matchAll(/public const string (\w+) = "([^"]+)";/g)].map(match => [match[1],match[2]]));
const moduleSource = await read('src/Aonik.SharedKernel/Modules/ModuleCatalog.cs');
const moduleCatalog = [...moduleSource.matchAll(/new\(\s*ModuleIds\.(\w+),[\s\S]*?IsCore: (true|false),\s*DependsOn: \[([^\]]*)\]/g)].map(match => ({
  id: moduleIds[match[1]], isCore: match[2] === 'true',
  dependsOn: [...match[3].matchAll(/ModuleIds\.(\w+)/g)].map(dependency => moduleIds[dependency[1]])
}));
if (moduleCatalog.length !== Object.keys(moduleIds).length || moduleCatalog.some(module => !module.id || module.dependsOn.some(id => !id))) throw new Error('Unrecognised module catalogue');
const screens = {};
// Registered screen metadata is a literal object; evaluate only that literal in an empty context.
// Keep this deliberately strict: a changed registry shape must fail rather than silently omit a screen.
for (const module of ['core', 'platform', 'finance', 'commerce', 'agent-command-center', 'agent-extensions']) {
  const source = await read(`src/Aonik.AdminUi/src/modules/${module}/index.ts`);
  const exportBlock = source.slice(source.indexOf('export const '));
  const declaredRequires = exportBlock.match(/\brequires:\s*(\[[^\]]*\])/);
  if (!declaredRequires) throw new Error(`Missing module requirements for ${module}`);
  const moduleRequires = vm.runInNewContext(declaredRequires[1]);
  for (const match of source.matchAll(/\bscreen:\s*{/g)) {
    const start = source.indexOf('{', match.index);
    let depth = 1, end = start + 1;
    for (; depth && end < source.length; end++) {
      if (source[end] === '{') depth++;
      if (source[end] === '}') depth--;
    }
    const definition = vm.runInNewContext(`(${source.slice(start, end)})`);
    const tail = source.slice(end, source.indexOf('\n', end));
    const route = tail.match(/path:\s*['"]([^'"]+)['"]/);
    if (!route) throw new Error(`Unrecognised route for ${definition.id}`);
    if (screens[definition.id]) throw new Error(`Duplicate screen ${definition.id}`);
    const requires = tail.match(/requires:\s*(\[[^\]]*\])/);
    screens[definition.id] = { ...definition, path: route[1], requires: [...moduleRequires, ...(requires ? vm.runInNewContext(requires[1]) : [])] };
  }
}
for (const profile of Object.values(profiles)) {
  for (const id of Object.keys(profile.screens)) {
    if (!screens[id]) throw new Error(`Missing registered screen ${id}`);
  }
}
const snapshot = JSON.stringify({ businessTypes, profiles, packs, moduleCatalog, screens }, null, 2) + '\n';
const outputs = [['design-system/semantic-tokens.css', tokenOutput], ['showcase/profile-snapshot.json', snapshot]];
for (const [name, content] of outputs) {
  const target = path.join(kit, name);
  if (process.argv.includes('--check')) {
    if (await readFile(target, 'utf8') !== content) throw new Error(`Out of date: ${name}. Run node scripts/sync-design.mjs`);
  } else {
    await mkdir(path.dirname(target), { recursive: true });
    await writeFile(target, content);
  }
}
console.log(`${process.argv.includes('--check') ? 'Verified' : 'Synced'} production tokens, ${Object.keys(profiles).length} profiles and ${Object.keys(screens).length} registered screens.`);
