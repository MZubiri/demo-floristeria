import { Injectable, signal } from '@angular/core';
import { type State } from './domain';
import { seed } from './seed';
const KEY='flore-demo-state-v1';
@Injectable({providedIn:'root'})
export class DemoStore {
 readonly state=signal<State>(seed());
 readonly warning=signal('');
 constructor(){
  try {
   const raw=localStorage.getItem(KEY);
   if(raw){
    const data=JSON.parse(raw);
    if(data.version!==1 || !Array.isArray(data.orders) || !Array.isArray(data.materials) || !Array.isArray(data.products) || !Array.isArray(data.expenses) || !Array.isArray(data.movements)) throw new Error('invalid');
    if(!data.orders.every((o:any)=>o.id && Array.isArray(o.items) && Array.isArray(o.payments) && Array.isArray(o.history))) throw new Error('invalid');
    this.state.set(data);
   }
  } catch { this.warning.set('No se pudo recuperar la demo guardada. Se muestran datos de ejemplo; exporta o reinicia si es necesario.'); }
 }
 commit(next:State){
  try { localStorage.setItem(KEY,JSON.stringify(next)); }
  catch { throw new Error('No hay espacio o el navegador bloquea el almacenamiento. La operación no se guardó. Exporta tus datos de demo.'); }
  this.state.set(next);
 }
 reset(){this.commit(seed());this.warning.set('');}
}
