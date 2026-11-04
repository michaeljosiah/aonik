import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import fs from 'node:fs/promises';

const kit=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const app=path.resolve(kit,'../../src/Aonik.AdminUi');
const require=createRequire(path.join(app,'package.json'));
const {build}=require('esbuild');
await build({entryPoints:[path.join(app,'src/pages/arke/publishing/PublishingDesignEntry.tsx')],outfile:path.join(kit,'screens/arke-publishing-ui.js'),
  bundle:true,format:'iife',globalName:'ArkePublishingUI',minify:false,jsx:'automatic',alias:{'@':path.join(app,'src')},
  banner:{js:'// Generated from the live publishing view and shared icons. Run scripts/build-publishing-design.mjs to regenerate.'},
  plugins:[{name:'canvas-react',setup(b){
    b.onResolve({filter:/^react(?:\/jsx-runtime)?$/},args=>({path:args.path,namespace:'canvas-react'}));
    b.onLoad({filter:/.*/,namespace:'canvas-react'},args=>({loader:'js',contents:args.path==='react'?`export const {useEffect,useId,useRef,useState}=globalThis.React;`:
      `export const Fragment=globalThis.React.Fragment;export function jsx(type,props,key){return globalThis.React.createElement(type,{...props,key});}export const jsxs=jsx;`}));
  }}]});
await fs.copyFile(path.join(app,'src/pages/arke/publishing/publishing.css'),path.join(kit,'design-system/publishing.css'));
console.log('Publishing design view and shared icon bundle rebuilt.');
