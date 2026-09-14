import { Component, Input, OnChanges, OnDestroy, signal } from '@angular/core';
import { IconComponent } from './icon.component';
interface PhotoMetadata {id:string;orderId:string;kind:string;name:string;date:string}
interface BinaryPhoto extends PhotoMetadata {bytes:ArrayBuffer;mimeType:string}
interface LegacyPhoto extends PhotoMetadata {blob:Blob}
type StoredPhoto = BinaryPhoto | LegacyPhoto;
interface PhotoView extends PhotoMetadata {blob:Blob;url:string}
function openDB():Promise<IDBDatabase>{
 return new Promise((resolve,reject)=>{
  const req=indexedDB.open('flore-demo-photos-v1',1);
  req.onupgradeneeded=()=>{const st=req.result.createObjectStore('photos',{keyPath:'id'});st.createIndex('orderId','orderId');};
  req.onsuccess=()=>resolve(req.result);req.onerror=()=>reject(new Error('El navegador no permite guardar fotos localmente.'));
 });
}
async function photoTask<T>(mode:IDBTransactionMode,action:(s:IDBObjectStore)=>IDBRequest<T>):Promise<T>{
 const db=await openDB();
 return new Promise((resolve,reject)=>{
  try {
   const tx=db.transaction('photos',mode);let result:T;
   tx.oncomplete=()=>{db.close();resolve(result);};
   tx.onabort=()=>{
    db.close();
    const message=tx.error?.name==='QuotaExceededError'
     ? 'No queda espacio para guardar la foto. Libera espacio en este dispositivo.'
     : 'El navegador no pudo guardar la fotografía localmente. Inténtalo de nuevo.';
    reject(new Error(message,{cause:tx.error}));
   };
   const req=action(tx.objectStore('photos'));
   req.onsuccess=()=>{result=req.result;};
  }catch(error){db.close();reject(error);}
 });
}
export async function clearPhotos(){await photoTask('readwrite',s=>s.clear());}
@Component({selector:'app-photos',standalone:true,imports:[IconComponent],template:`
 <div class="section-heading"><div><h3>Fotografías del pedido</h3><p>Referencia, resultado final y entrega.</p></div><span class="badge neutral">{{photos().length}} / 4</span></div>
 <div class="photo-controls">
 <label class="sr-only" for="photo-kind">Tipo de fotografía</label><select id="photo-kind" #kind><option>Referencia</option><option>Arreglo final</option><option>Entrega</option></select>
 <button class="button secondary small" type="button" (click)="camera.click()" [disabled]="busy() || photos().length>=4"><app-icon name="camera"/>Tomar foto</button>
 <button class="button secondary small" type="button" (click)="gallery.click()" [disabled]="busy() || photos().length>=4"><app-icon name="image"/>Elegir fotos</button>
 <input #camera hidden data-testid="camera-input" type="file" accept="image/*,.heic,.heif" capture="environment" (change)="choose($event,kind.value)">
 <input #gallery hidden data-testid="gallery-input" type="file" accept="image/*,.heic,.heif" multiple (change)="choose($event,kind.value)">
 </div>
 @if(busy()){<p class="notice" role="status">Procesando fotografía en este dispositivo…</p>}
 @if(error()){<p class="error" role="alert">{{error()}}</p>}
 <div class="photo-grid">
 @for(p of photos();track p.id){<figure><a [href]="p.url" target="_blank" rel="noopener" aria-label="Abrir fotografía"><img [src]="p.url" [alt]="p.kind+' del pedido'" loading="lazy"></a><figcaption><span>{{p.kind}}<small>{{size(p.blob.size)}} · Local</small></span><button class="icon-button danger-text" type="button" aria-label="Eliminar fotografía" (click)="remove(p)" [disabled]="busy()"><app-icon name="trash"/></button></figcaption></figure>}
 </div>
 @if(!photos().length && !busy()){<div class="empty-photo"><app-icon name="camera"/><p>El detalle también está en las fotos.</p><small>Agrega una imagen desde tu iPhone o computador.</small></div>}
 <p class="micro">Demo local: estas fotos no se suben a un servidor. Máx. 20 MB por archivo. Si HEIC no se abre, utiliza JPEG.</p>
`})
export class PhotosComponent implements OnChanges,OnDestroy {
 @Input({required:true}) orderId=''; photos=signal<PhotoView[]>([]);busy=signal(false);error=signal('');private disposed=false;
 ngOnChanges(){this.load();}
 async load(){const id=this.orderId;try{
  const photos=await photoTask<StoredPhoto[]>('readonly',s=>s.index('orderId').getAll(id));
  if(this.disposed||id!==this.orderId)return;
  const views=photos.map(p=>{
   // Read earlier Blob records as well as the portable binary representation.
   const blob='bytes' in p?new Blob([p.bytes],{type:p.mimeType}):p.blob;
   return {...p,blob,url:URL.createObjectURL(blob)};
  });
  this.release();this.photos.set(views);
 }catch(e){this.error.set((e as Error).message);}}
 size(bytes:number){return Math.round(bytes/1024)+' KB';}
 async compress(file:File):Promise<Blob>{
  if(file.size>20*1024*1024)throw new Error('La foto supera 20 MB. Selecciona una versión más pequeña.');
  if(!/^image\/(jpeg|png|webp|heic|heif)$/.test(file.type) && !/\.(jpe?g|png|webp|heic|heif)$/i.test(file.name)) throw new Error('Utiliza JPEG, PNG, WebP o HEIC compatible. No se admiten SVG, RAW ni videos.');
  const bytes=new Uint8Array(await file.slice(0,40).arrayBuffer());
  const ascii=(start:number,len:number)=>String.fromCharCode(...bytes.slice(start,start+len));
  const jpeg=bytes[0]===255&&bytes[1]===216&&bytes[2]===255;
  const png=bytes[0]===137&&ascii(1,3)==='PNG'&&bytes[4]===13&&bytes[5]===10;
  const webp=ascii(0,4)==='RIFF'&&ascii(8,4)==='WEBP';
  const heif=ascii(4,4)==='ftyp'&&['heic','heix','hevc','hevx','mif1','msf1'].some(b=>ascii(8,32).includes(b));
  if(!jpeg&&!png&&!webp&&!heif)throw new Error('El contenido del archivo no es una fotografía compatible. No basta con cambiar su extensión.');
  const url=URL.createObjectURL(file);
  try {
   const img=new Image();img.src=url;
   try{await img.decode();}catch{throw new Error('Este navegador no pudo abrir la foto. Prueba una versión JPEG; la conversión HEIC en servidor no está incluida en la demo.');}
   if(img.naturalWidth*img.naturalHeight>50000000)throw new Error('La foto supera 50 megapíxeles. Selecciona una versión de menor resolución.');
   const ratio=Math.min(1,1600/Math.max(img.naturalWidth,img.naturalHeight)), canvas=document.createElement('canvas');
   canvas.width=Math.round(img.naturalWidth*ratio);canvas.height=Math.round(img.naturalHeight*ratio);
   const ctx=canvas.getContext('2d');if(!ctx)throw new Error('No se pudo procesar la imagen.');
   ctx.fillStyle='#ffffff';ctx.fillRect(0,0,canvas.width,canvas.height);ctx.drawImage(img,0,0,canvas.width,canvas.height);
   return await new Promise<Blob>((resolve,reject)=>canvas.toBlob(b=>b?resolve(b):reject(new Error('Falló la compresión.')),'image/jpeg',0.83));
  }finally{URL.revokeObjectURL(url);}
 }
 async choose(event:Event,kind:string){
  const input=event.target as HTMLInputElement, files=Array.from(input.files||[]);
  if(!files.length||this.busy())return;this.busy.set(true);this.error.set('');
  const room=4-this.photos().length;
  try{
   for(const file of files.slice(0,room)){
    const blob=await this.compress(file);if(this.disposed)break;
    // Materialize bytes before opening the transaction; WebKit's temporary
    // contexts can reject Blob persistence even when image decoding succeeds.
    const bytes=await blob.arrayBuffer();if(this.disposed)break;
    const photo:BinaryPhoto={id:crypto.randomUUID(),orderId:this.orderId,kind,name:file.name,bytes,mimeType:blob.type,date:new Date().toISOString()};
    await photoTask('readwrite',s=>s.add(photo));
   }
   if(files.length>room)this.error.set('Solo se guardaron las fotos que caben en el límite de 4 por pedido.');
  }catch(e){this.error.set((e as Error).message);}
  finally{input.value='';if(!this.disposed){await this.load();this.busy.set(false);}}
 }
 async remove(photo:PhotoView){if(!confirm('¿Eliminar esta fotografía local de la demo?'))return;try{await photoTask('readwrite',s=>s.delete(photo.id));await this.load();}catch(e){this.error.set((e as Error).message);}}
 release(){this.photos().forEach(p=>URL.revokeObjectURL(p.url));}
 ngOnDestroy(){this.disposed=true;this.release();}
}
