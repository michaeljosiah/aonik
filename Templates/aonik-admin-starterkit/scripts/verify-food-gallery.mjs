import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../marketing');
const panel={innerHTML:'',insertAdjacentHTML(_where,html){this.innerHTML+=html;}};
const source=fs.readFileSync(path.join(root,'index.html'),'utf8');
const script=source.match(/<script type="module">([\s\S]*?)<\/script>/)[1];
vm.runInNewContext(script,{document:{querySelector(){return panel;}}});
let checked=0;
for(const html of [panel.innerHTML,fs.readFileSync(path.join(root,'food-business.html'),'utf8'),fs.readFileSync(path.join(root,'arke-worlds.html'),'utf8')]){
  for(const match of html.matchAll(/(?:src|href)="([^"]+)"/g)){
    const target=match[1];
    if(target.startsWith('#')||target.startsWith('http'))continue;
    const file=path.resolve(root,target.split(/[?#]/)[0]);
    if(!fs.existsSync(file))throw Error('Missing gallery target: '+target);
    checked++;
  }
}
console.log(`Verified ${checked} gallery asset and document links.`);
