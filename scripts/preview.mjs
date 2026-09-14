import { createServer } from 'node:http';
import { readFile,stat } from 'node:fs/promises';
import { resolve,extname } from 'node:path';
const root=resolve('dist/flore/browser');
const types={'.html':'text/html','.js':'text/javascript','.css':'text/css','.svg':'image/svg+xml','.webmanifest':'application/manifest+json','.json':'application/json'};
const server=createServer(async(req,res)=>{
 try{
  const path=decodeURIComponent(new URL(req.url,'http://localhost').pathname);
  let file=resolve(root,'.'+path);
  if(file!==root&&!file.startsWith(root+'/')){res.writeHead(403);res.end();return;}
  try{if((await stat(file)).isDirectory())file=resolve(file,'index.html');}
  catch{if(extname(path)){res.writeHead(404);res.end();return;}file=resolve(root,'index.html');}
  res.writeHead(200,{'Content-Type':types[extname(file)]||'application/octet-stream'});res.end(await readFile(file));
 }catch{res.writeHead(500);res.end('Error');}
});
server.listen(4173,'127.0.0.1',()=>console.log('Demo preview http://127.0.0.1:4173'));
