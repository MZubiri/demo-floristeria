export type OrderStatus = 'recibido' | 'confirmado' | 'preparacion' | 'listo' | 'camino' | 'entregado' | 'cancelado';
export const STATUSES: { value: OrderStatus; label: string; color: string }[] = [
 {value:'recibido',label:'Recibido',color:'neutral'}, {value:'confirmado',label:'Confirmado',color:'blue'},
 {value:'preparacion',label:'En preparación',color:'amber'}, {value:'listo',label:'Listo para entregar',color:'sage'},
 {value:'camino',label:'En camino',color:'purple'}, {value:'entregado',label:'Entregado',color:'green'},
 {value:'cancelado',label:'Cancelado',color:'rose'}
];
export const METHODS = ['Efectivo','Nequi','Bancolombia','Transferencia','Tarjeta'];
export interface Material { id:string; name:string; category:string; unit:string; stock:number; minimum:number; cost:number; supplier:string }
export interface Ingredient { materialId:string; quantity:number; unitCost:number }
export interface Product { id:string; name:string; category:string; price:number; labor:number; image:string; description:string; recipe:Ingredient[] }
export interface Line { id:string; productId:string; name:string; quantity:number; price:number; labor:number; recipe:Ingredient[]; notes:string }
export interface Payment { id:string; amount:number; method:string; date:string; reference:string }
export interface History { date:string; title:string; note:string }
export interface Order {
 id:string; number:string; createdAt:string; customer:string; phone:string; email:string;
 recipient:string; recipientPhone:string; address:string; area:string; deliveryDate:string; time:string;
 deliveryMethod:string; priority:string; items:Line[]; discount:number; shipping:number;
 hasCard:boolean; cardMessage:string; notes:string; status:OrderStatus; payments:Payment[];
 history:History[]; consumed:boolean; deliveredAt?:string; receivedBy?:string; deliveryNote?:string;
}
export interface Movement { id:string; materialId:string; type:'entrada'|'consumo'|'merma'; quantity:number; cost:number; date:string; reason:string; orderId?:string }
export interface Expense { id:string; category:string; description:string; amount:number; date:string; method:string }
export interface State { version:1; orders:Order[]; materials:Material[]; products:Product[]; movements:Movement[]; expenses:Expense[]; business:string }
export function uid():string { return crypto.randomUUID(); }
export function dayKey(date=new Date()):string {
 return date.getFullYear()+'-'+String(date.getMonth()+1).padStart(2,'0')+'-'+String(date.getDate()).padStart(2,'0');
}
export function offsetDay(offset:number):string { const d=new Date(); d.setDate(d.getDate()+offset); return dayKey(d); }
export function total(o:Order):number { return Math.round(o.items.reduce((n,i)=>n+i.price*i.quantity,0)-o.discount+o.shipping); }
export function paid(o:Order):number { return o.payments.reduce((n,p)=>n+p.amount,0); }
export function balance(o:Order):number { return Math.max(0,total(o)-paid(o)); }
export function lineCost(l:Line):number { return l.labor+l.recipe.reduce((n,r)=>n+r.quantity*r.unitCost,0); }
export function orderCost(o:Order):number { return Math.round(o.items.reduce((n,l)=>n+lineCost(l)*l.quantity,0)); }
export function paymentLabel(o:Order):string { return balance(o)===0?'Pagado':paid(o)>0?'Abonado':'Sin pagar'; }
export function requirements(o:Order):Map<string,number> {
 const out=new Map<string,number>();
 for(const l of o.items) for(const r of l.recipe) out.set(r.materialId,(out.get(r.materialId)||0)+r.quantity*l.quantity);
 return out;
}
export function reserved(state:State,id:string,excludeOrder?:string):number {
 return state.orders.filter(o=>o.id!==excludeOrder && o.status==='confirmado' && !o.consumed)
 .reduce((n,o)=>n+(requirements(o).get(id)||0),0);
}
export function available(state:State,id:string,excludeOrder?:string):number {
 const m=state.materials.find(m=>m.id===id); return (m?.stock||0)-reserved(state,id,excludeOrder);
}
function positive(n:number,label:string,allowZero=false):void {
 if(!Number.isFinite(n) || (allowZero?n<0:n<=0)) throw new Error(label+' debe ser '+(allowZero?'cero o mayor.':'mayor que cero.'));
}
function checkStock(state:State,o:Order):void {
 for(const [id,q] of requirements(o)) {
  const m=state.materials.find(m=>m.id===id);
  if(!m || available(state,id,o.id)+0.0001<q) throw new Error('No hay suficiente inventario disponible de '+(m?.name||id)+'.');
 }
}
export function validateOrder(o:Order):void {
 if(!o.customer.trim() || !o.phone.trim() || !o.recipient.trim()) throw new Error('Completa cliente, teléfono y destinatario.');
 if(!/^\+?[\d\s()\-]{7,20}$/.test(o.phone.trim())) throw new Error('Revisa el teléfono del cliente.');
 if(!/^\d{4}-\d{2}-\d{2}$/.test(o.deliveryDate) || !/^\d{2}:\d{2}$/.test(o.time)) throw new Error('Selecciona una fecha y hora de entrega.');
 if(o.deliveryMethod==='Domicilio' && !o.address.trim()) throw new Error('Falta la dirección de entrega.');
 if(o.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(o.email)) throw new Error('Revisa el correo electrónico.');
 if(!o.items.length) throw new Error('Agrega al menos un arreglo.');
 for(const l of o.items) {
  if(!l.name.trim()) throw new Error('El arreglo necesita un nombre.');
  positive(l.quantity,'La cantidad'); if(!Number.isInteger(l.quantity)) throw new Error('La cantidad de arreglos debe ser entera.');
  positive(l.price,'El precio'); positive(l.labor,'La mano de obra',true);
  for(const r of l.recipe) { positive(r.quantity,'El consumo de material',true); positive(r.unitCost,'El costo',true); }
 }
 positive(o.discount,'El descuento',true); positive(o.shipping,'El domicilio',true);
 const subtotal=o.items.reduce((n,l)=>n+l.price*l.quantity,0);
 if(o.discount>subtotal) throw new Error('El descuento supera el subtotal.');
 if(total(o)<paid(o)) throw new Error('El total no puede ser menor que los abonos ya registrados.');
}
export function saveOrder(state:State,order:Order):State {
 validateOrder(order);
 const next=structuredClone(state), index=next.orders.findIndex(o=>o.id===order.id), current=next.orders[index];
 if(current && ['entregado','cancelado'].includes(current.status)) throw new Error('El pedido está cerrado.');
 if(current?.consumed && JSON.stringify(current.items)!==JSON.stringify(order.items)) throw new Error('Los materiales ya fueron consumidos. En esta demo solo puedes editar entrega, tarjeta y notas.');
 const copy=structuredClone(order);
 if(current) {
  copy.history=current.history.concat({date:new Date().toISOString(),title:'Pedido actualizado',note:current.deliveryDate!==copy.deliveryDate || current.time!==copy.time?'Entrega reprogramada: '+copy.deliveryDate+' '+copy.time:'Datos, personalización o entrega actualizados.'});
 }
 if(copy.status==='confirmado' && !copy.consumed) checkStock(next,copy);
 if(index<0) next.orders.unshift(copy); else next.orders[index]=copy;
 return next;
}
export function addPayment(state:State,id:string,amount:number,method:string,reference=''):State {
 positive(amount,'El abono');
 if(!Number.isInteger(amount)) throw new Error('Registra el abono en pesos enteros.');
 const next=structuredClone(state), o=next.orders.find(o=>o.id===id);
 if(!o || o.status==='cancelado') throw new Error('Este pedido no admite pagos.');
 if(amount>balance(o)) throw new Error('El abono supera el saldo pendiente.');
 if(!METHODS.includes(method)) throw new Error('Selecciona un método de pago válido.');
 o.payments.push({id:uid(),amount:Math.round(amount),method,reference,date:new Date().toISOString()});
 o.history.push({date:new Date().toISOString(),title:'Abono registrado',note:method+' · '+Math.round(amount)+' COP'});
 return next;
}
export function nextStatus(o:Order):OrderStatus|undefined {
 const list:OrderStatus[]=['recibido','confirmado','preparacion','listo','camino','entregado'];
 if(o.status==='cancelado' || o.status==='entregado') return undefined;
 if(o.status==='listo' && o.deliveryMethod==='Recoger en tienda') return 'entregado';
 return list[list.indexOf(o.status)+1];
}
export function transition(state:State,id:string,target:OrderStatus,receipt='',note='',now=new Date().toISOString()):State {
 const next=structuredClone(state), o=next.orders.find(o=>o.id===id);
 if(!o) throw new Error('Pedido no encontrado.');
 if(o.status===target) return next; // Idempotent retries must never consume twice.
 if(o.status==='entregado' || o.status==='cancelado') throw new Error('El pedido ya está cerrado.');
 if(target!=='cancelado' && nextStatus(o)!==target) throw new Error('Transición de pedido no válida.');
 if(target==='confirmado' || target==='preparacion') checkStock(next,o);
 if(target==='preparacion' && !o.consumed) {
  for(const [mid,q] of requirements(o)) {
   const m=next.materials.find(m=>m.id===mid)!; m.stock=Math.round((m.stock-q)*1000)/1000;
   next.movements.unshift({id:uid(),materialId:mid,type:'consumo',quantity:q,cost:m.cost,date:now,reason:'Preparación '+o.number,orderId:o.id});
  }
  o.consumed=true;
 }
 if(target==='entregado') {
  if(!receipt.trim()) throw new Error('Indica quién recibió el pedido.');
  o.deliveredAt=now; o.receivedBy=receipt.trim(); o.deliveryNote=note;
 }
 if(target==='cancelado' && paid(o)>0) throw new Error('Este pedido tiene abonos. La devolución bancaria requiere el sistema de producción; no se cancela ni se borra el dinero en esta demo.');
 o.status=target;
 o.history.push({date:now,title:STATUSES.find(s=>s.value===target)!.label,note:note || (target==='cancelado' && o.consumed?'Los materiales consumidos no vuelven a inventario.':'Actualizado por Administradora demo')});
 return next;
}
export function moveStock(state:State,id:string,type:'entrada'|'merma',quantity:number,reason:string,cost?:number):State {
 positive(quantity,'La cantidad'); if(!reason.trim()) throw new Error('Escribe un motivo o referencia.');
 const next=structuredClone(state), m=next.materials.find(m=>m.id===id);
 if(!m) throw new Error('Material no encontrado.');
 if(type==='merma' && quantity>available(next,id)) throw new Error('La merma supera las existencias disponibles. Revisa primero los pedidos reservados.');
 const purchaseCost=cost ?? m.cost; positive(purchaseCost,'El costo unitario',true);
 const previousCost=m.cost;
 if(type==='entrada') m.cost=(m.stock*m.cost+quantity*purchaseCost)/(m.stock+quantity);
 m.stock=Math.round((m.stock+(type==='entrada'?quantity:-quantity))*1000)/1000;
 next.movements.unshift({id:uid(),materialId:id,type,quantity,cost:type==='entrada'?purchaseCost:previousCost,date:new Date().toISOString(),reason:reason.trim()});
 return next;
}
export function newLine(p:Product,materials:Material[]):Line {
 return {id:uid(),productId:p.id,name:p.name,quantity:1,price:p.price,labor:p.labor,notes:'',recipe:p.recipe.map(r=>({...r,unitCost:materials.find(m=>m.id===r.materialId)?.cost||r.unitCost}))};
}
export function blankOrder(state:State):Order {
 const max=state.orders.reduce((n,o)=>Math.max(n,Number(o.number.replace(/\D/g,''))||0),1000);
 const now=new Date().toISOString();
 return {id:uid(),number:'FL-'+(max+1),createdAt:now,customer:'',phone:'',email:'',recipient:'',recipientPhone:'',address:'',area:'',deliveryDate:dayKey(),time:'15:00',deliveryMethod:'Domicilio',priority:'Normal',items:[],discount:0,shipping:12000,hasCard:true,cardMessage:'',notes:'',status:'recibido',payments:[],history:[{date:now,title:'Pedido creado',note:'Administradora demo'}],consumed:false};
}
export function inPeriod(date:string,from:string,to:string):boolean { const day=dayKey(new Date(date)); return day>=from && day<=to; }
export function summary(state:State,from:string,to:string) {
 const sales=state.orders.filter(o=>o.status==='entregado' && o.deliveredAt && inPeriod(o.deliveredAt,from,to));
 const revenue=sales.reduce((n,o)=>n+total(o),0), cost=sales.reduce((n,o)=>n+orderCost(o),0);
 const expenses=state.expenses.filter(e=>e.date>=from && e.date<=to).reduce((n,e)=>n+e.amount,0);
 const waste=state.movements.filter(m=>m.type==='merma' && inPeriod(m.date,from,to)).reduce((n,m)=>n+m.quantity*m.cost,0);
 const canceledCost=state.orders.filter(o=>o.status==='cancelado'&&o.consumed&&inPeriod(o.history[o.history.length-1].date,from,to)).reduce((n,o)=>n+orderCost(o),0);
 const collected=state.orders.flatMap(o=>o.payments).filter(p=>inPeriod(p.date,from,to)).reduce((n,p)=>n+p.amount,0);
 return {revenue,cost,expenses,waste,canceledCost,collected,profit:revenue-cost-expenses-waste-canceledCost,sales:sales.length};
}
export function csvCell(value:unknown):string {
 let v=String(value??''); if(/^[=+\-@\t\r]/.test(v)) v="'"+v;
 return '"'+v.replace(/"/g,'""')+'"';
}
