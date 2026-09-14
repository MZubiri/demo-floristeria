import { Component, computed, HostListener, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IconComponent } from './icon.component';
import { DialogComponent } from './dialog.component';
import { PhotosComponent, clearPhotos } from './photos.component';
import { OrderFormComponent } from './order-form.component';
import { DemoStore } from './store';
import { type Order, type OrderStatus, type Product, type Material, STATUSES, METHODS, total, paid, balance, orderCost, paymentLabel, nextStatus, reserved, available, dayKey, offsetDay, blankOrder, newLine, saveOrder, addPayment, transition, moveStock, summary, csvCell, uid } from './domain';
type View='inicio'|'pedidos'|'agenda'|'catalogo'|'inventario'|'pagos'|'gastos'|'contactos'|'informes'|'ajustes';
@Component({selector:'app-root',standalone:true,imports:[FormsModule,IconComponent,DialogComponent,PhotosComponent,OrderFormComponent],templateUrl:'./app.component.html'})
export class AppComponent {
 readonly store=inject(DemoStore);readonly state=this.store.state;
 readonly navItems:{id:View;label:string;icon:string;group:string}[]=[
 {id:'inicio',label:'Inicio',icon:'home',group:'TU FLORISTERÍA'},
 {id:'pedidos',label:'Pedidos',icon:'orders',group:''},
 {id:'agenda',label:'Agenda de entregas',icon:'calendar',group:''},
 {id:'catalogo',label:'Catálogo floral',icon:'flower',group:''},
 {id:'inventario',label:'Inventario y merma',icon:'stock',group:''},
 {id:'pagos',label:'Ventas y pagos',icon:'wallet',group:'TU NEGOCIO'},
 {id:'gastos',label:'Gastos',icon:'expense',group:''},
 {id:'contactos',label:'Clientes y proveedores',icon:'users',group:''},
 {id:'informes',label:'Informes',icon:'chart',group:''},
 {id:'ajustes',label:'Configuración',icon:'settings',group:''}
 ];
 readonly view=signal<View>('inicio');menuOpen=false;query='';dateFilter='all';statusFilter='';pendingOnly=false;
 readonly selectedId=signal('');readonly selected=computed(()=>this.state().orders.find(o=>o.id===this.selectedId()));
 readonly draft=signal<Order|null>(null);editing=false;detailTab='resumen';error=signal('');toast=signal('');
 private toastTimer?:ReturnType<typeof setTimeout>;
 total=total;paid=paid;balance=balance;orderCost=orderCost;paymentLabel=paymentLabel;nextStatus=nextStatus;statuses=STATUSES;methods=METHODS;
 today=dayKey();readonly daily=computed(()=>summary(this.state(),this.today,this.today));
 readonly week=computed(()=>summary(this.state(),offsetDay(-6),this.today));
 readonly activeOrders=computed(()=>this.state().orders.filter(o=>!['entregado','cancelado'].includes(o.status)));
 readonly lowStock=computed(()=>this.state().materials.filter(m=>available(this.state(),m.id)<=m.minimum));
 readonly due=computed(()=>this.activeOrders().filter(o=>o.deliveryDate===this.today).sort((a,b)=>a.time.localeCompare(b.time)));
 readonly receivables=computed(()=>this.state().orders.filter(o=>o.status!=='cancelado').reduce((n,o)=>n+balance(o),0));
 detailAmount=0;detailMethod='Nequi';detailReference='';receipt='';deliveryNote='';
 calendarMonth=new Date(new Date().getFullYear(),new Date().getMonth(),1);calendarDay=dayKey();
 stockQuery='';stockCategory='';onlyLow=false;stockTab='materiales';stockModal=false;
 movement={materialId:'rosa',type:'merma' as 'entrada'|'merma',quantity:1,cost:3000,reason:''};
 expenseModal=false;expense={category:'Domicilios',description:'',amount:0,date:dayKey(),method:'Efectivo'};
 readonly expenseCategories=['Domicilios','Transporte','Trabajadores','Servicios','Arriendo','Publicidad','Mantenimiento','Otros'];
 contactTab='clientes';contactQuery='';catalogQuery='';catalogCategory='';
 reportFrom=offsetDay(-29);reportTo=dayKey();paymentQuery='';onlyBalance=true;expenseFrom=offsetDay(-29);expenseTo=dayKey();
 constructor(){this.onHash();}
 @HostListener('window:hashchange') onHash(){const hash=location.hash.slice(1) as View;if(this.navItems.some(n=>n.id===hash))this.view.set(hash);}
 go(v:View){this.view.set(v);location.hash=v;this.menuOpen=false;this.error.set('');window.scrollTo({top:0,behavior:'instant'});}
 get pageTitle(){return this.navItems.find(n=>n.id===this.view())?.label||'Inicio';}
 get todayLabel(){return new Date().toLocaleDateString('es-CO',{weekday:'long',day:'numeric',month:'long'});}
 money(n:number){return new Intl.NumberFormat('es-CO',{style:'currency',currency:'COP',maximumFractionDigits:0}).format(n||0);}
 number(n:number){return new Intl.NumberFormat('es-CO',{maximumFractionDigits:1}).format(n);}
 shortDate(s:string){return new Date(s.length===10?s+'T12:00:00':s).toLocaleDateString('es-CO',{day:'numeric',month:'short'});}
 dateTime(s:string){return new Date(s).toLocaleString('es-CO',{day:'numeric',month:'short',hour:'2-digit',minute:'2-digit'});}
 label(s:OrderStatus){return STATUSES.find(x=>x.value===s)?.label||s;}
 color(s:OrderStatus){return STATUSES.find(x=>x.value===s)?.color||'neutral';}
 initials(name:string){return name.split(' ').slice(0,2).map(n=>n[0]).join('');}
 image(o:Order){return this.state().products.find(p=>p.id===o.items[0]?.productId)?.image||'assets/blancas.svg';}
 search(text:string,query:string){return text.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().includes(query.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase());}
 get filteredOrders(){
  return this.state().orders.filter(o=>this.search([o.number,o.customer,o.phone,o.recipient,...o.items.map(i=>i.name)].join(' '),this.query))
  .filter(o=>!this.statusFilter||o.status===this.statusFilter)
  .filter(o=>!this.pendingOnly||balance(o)>0&&o.status!=='cancelado')
  .filter(o=>this.dateFilter==='all'||(this.dateFilter==='today'&&o.deliveryDate===this.today)||(this.dateFilter==='tomorrow'&&o.deliveryDate===offsetDay(1))||(this.dateFilter==='late'&&o.deliveryDate<this.today&&!['entregado','cancelado'].includes(o.status)));
 }
 open(o:Order){this.selectedId.set(o.id);this.detailTab='resumen';this.error.set('');this.detailAmount=balance(o);this.receipt=o.recipient;this.deliveryNote='';this.detailReference='';}
 closeDetail(){this.selectedId.set('');this.error.set('');}
 newOrder(p?:Product){const d=blankOrder(this.state());if(p)d.items=[newLine(p,this.state().materials)];this.editing=false;this.closeDetail();this.draft.set(d);}
 edit(o:Order){this.editing=true;this.closeDetail();this.draft.set(structuredClone(o));}
 saveDraft(o:Order){try{this.store.commit(saveOrder(this.state(),o));this.draft.set(null);this.open(this.state().orders.find(x=>x.id===o.id)!);this.notify('Pedido guardado en este navegador.');}catch(e){this.error.set((e as Error).message);}}
 change(o:Order,status:OrderStatus){
  if(status==='cancelado' && !confirm('¿Cancelar '+o.number+'? Se liberan reservas; los materiales ya consumidos no se recuperan.'))return;
  try{this.store.commit(transition(this.state(),o.id,status,this.receipt,this.deliveryNote));this.error.set('');this.notify('Pedido actualizado: '+this.label(status));}catch(e){this.error.set((e as Error).message);}
 }
 pay(o:Order){
  try{this.store.commit(addPayment(this.state(),o.id,Number(this.detailAmount),this.detailMethod,this.detailReference));this.detailAmount=balance(this.state().orders.find(x=>x.id===o.id)!);this.error.set('');this.notify('Abono registrado. No se realizó ningún cobro bancario.');}catch(e){this.error.set((e as Error).message);}
 }
 notify(text:string){this.toast.set(text);if(this.toastTimer)clearTimeout(this.toastTimer);this.toastTimer=setTimeout(()=>this.toast.set(''),5000);}
 get bars(){const amounts=Array.from({length:7},(_,i)=>({day:offsetDay(i-6),value:summary(this.state(),offsetDay(i-6),offsetDay(i-6)).revenue}));const max=Math.max(...amounts.map(b=>b.value),1);return amounts.map(b=>({...b,height:Math.max(3,b.value/max*100),label:new Date(b.day+'T12:00:00').toLocaleDateString('es-CO',{weekday:'short'})}));}
 get calendarLabel(){return this.calendarMonth.toLocaleDateString('es-CO',{month:'long',year:'numeric'});}
 get calendarCells(){const start=new Date(this.calendarMonth);start.setDate(1-(start.getDay()+6)%7);return Array.from({length:42},(_,i)=>{const d=new Date(start);d.setDate(start.getDate()+i);const key=dayKey(d);return {key,day:d.getDate(),current:d.getMonth()===this.calendarMonth.getMonth(),count:this.state().orders.filter(o=>o.deliveryDate===key&&o.status!=='cancelado').length};});}
 shiftMonth(n:number){this.calendarMonth=new Date(this.calendarMonth.getFullYear(),this.calendarMonth.getMonth()+n,1);}
 resetCalendar(){this.calendarMonth=new Date(new Date().getFullYear(),new Date().getMonth(),1);this.calendarDay=dayKey();}
 get dayOrders(){return this.state().orders.filter(o=>o.deliveryDate===this.calendarDay&&o.status!=='cancelado').sort((a,b)=>a.time.localeCompare(b.time));}
 get filteredProducts(){return this.state().products.filter(p=>this.search(p.name,this.catalogQuery)&&(!this.catalogCategory||p.category===this.catalogCategory));}
 productCost(p:Product){return p.labor+p.recipe.reduce((n,r)=>n+r.quantity*(this.state().materials.find(m=>m.id===r.materialId)?.cost||0),0);}
 available(m:Material){return available(this.state(),m.id);}
 reserved(m:Material){return reserved(this.state(),m.id);}
 materialName(id:string){return this.state().materials.find(m=>m.id===id)?.name||id;}
 get filteredMaterials(){return this.state().materials.filter(m=>this.search(m.name,this.stockQuery)&&(!this.stockCategory||m.category===this.stockCategory)&&(!this.onlyLow||available(this.state(),m.id)<=m.minimum));}
 get inventoryValue(){return this.state().materials.reduce((n,m)=>n+m.stock*m.cost,0);}
 openStock(type:'entrada'|'merma',id='rosa'){const m=this.state().materials.find(m=>m.id===id)!;this.movement={materialId:id,type,quantity:1,cost:m.cost,reason:''};this.error.set('');this.stockModal=true;}
 selectStockMaterial(){this.movement.cost=this.state().materials.find(m=>m.id===this.movement.materialId)?.cost||0;}
 saveStock(){try{this.store.commit(moveStock(this.state(),this.movement.materialId,this.movement.type,Number(this.movement.quantity),this.movement.reason,Number(this.movement.cost)));this.stockModal=false;this.notify('Movimiento de inventario registrado.');}catch(e){this.error.set((e as Error).message);}}
 get paymentOrders(){return this.state().orders.filter(o=>o.status!=='cancelado'&&(!this.onlyBalance||balance(o)>0)&&this.search(o.customer+' '+o.number,this.paymentQuery));}
 get allPayments(){return this.state().orders.flatMap(o=>o.payments.map(p=>({...p,order:o.number,customer:o.customer}))).sort((a,b)=>b.date.localeCompare(a.date));}
 openExpense(){this.expense={category:'Domicilios',description:'',amount:0,date:dayKey(),method:'Efectivo'};this.error.set('');this.expenseModal=true;}
 saveExpense(){try{
  const e=this.expense;if(!e.description.trim()||!e.date||!Number.isFinite(Number(e.amount))||Number(e.amount)<=0)throw new Error('Completa la descripción, fecha y un valor mayor que cero.');
  this.store.commit({...this.state(),expenses:[{...e,id:uid(),amount:Math.round(Number(e.amount))},...this.state().expenses]});this.expenseModal=false;this.notify('Gasto registrado.');
 }catch(e){this.error.set((e as Error).message);}}
 get filteredExpenses(){return this.state().expenses.filter(e=>e.date>=this.expenseFrom&&e.date<=this.expenseTo).sort((a,b)=>b.date.localeCompare(a.date));}
 get expenseTotal(){return this.filteredExpenses.reduce((n,e)=>n+e.amount,0);}
 get clients(){const map=new Map<string,{name:string;phone:string;email:string;orders:number;spent:number;balance:number}>();for(const o of this.state().orders){let c=map.get(o.phone);if(!c){c={name:o.customer,phone:o.phone,email:o.email,orders:0,spent:0,balance:0};map.set(o.phone,c);}c.orders++;if(o.status==='entregado')c.spent+=total(o);if(o.status!=='cancelado')c.balance+=balance(o);}return Array.from(map.values()).filter(c=>this.search(c.name+' '+c.phone,this.contactQuery));}
 clientOrders(phone:string){this.query=phone;this.dateFilter='all';this.statusFilter='';this.pendingOnly=false;this.go('pedidos');}
 get suppliers(){return Array.from(new Set(this.state().materials.map(m=>m.supplier))).filter(s=>this.search(s,this.contactQuery)).map(name=>({name,materials:this.state().materials.filter(m=>m.supplier===name),purchases:this.state().movements.filter(m=>m.type==='entrada'&&this.state().materials.find(x=>x.id===m.materialId)?.supplier===name).reduce((n,m)=>n+m.quantity*m.cost,0)}));}
 get report(){return summary(this.state(),this.reportFrom,this.reportTo);}
 get rankings(){const rows=new Map<string,{name:string;quantity:number;revenue:number;cost:number}>();for(const o of this.state().orders){if(o.status!=='entregado'||!o.deliveredAt)continue;const d=dayKey(new Date(o.deliveredAt));if(d<this.reportFrom||d>this.reportTo)continue;for(const i of o.items){let p=rows.get(i.productId);if(!p){p={name:i.name,quantity:0,revenue:0,cost:0};rows.set(i.productId,p);}p.quantity+=i.quantity;p.revenue+=i.price*i.quantity;p.cost+=(i.labor+i.recipe.reduce((n,r)=>n+r.quantity*r.unitCost,0))*i.quantity;}}return Array.from(rows.values()).sort((a,b)=>b.quantity-a.quantity);}
 get expenseGroups(){return this.expenseCategories.map(name=>({name,value:this.state().expenses.filter(e=>e.category===name&&e.date>=this.reportFrom&&e.date<=this.reportTo).reduce((n,e)=>n+e.amount,0)})).filter(g=>g.value>0);}
 range(days:number){this.reportFrom=offsetDay(-days+1);this.reportTo=dayKey();}
 download(name:string,content:string,type:string){const url=URL.createObjectURL(new Blob([content],{type})),a=document.createElement('a');a.href=url;a.download=name;document.body.appendChild(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),1000);}
 exportOrders(){const rows=[['Pedido','Cliente','Destinatario','Entrega','Hora','Estado','Total COP','Abonos COP','Saldo COP'],...this.filteredOrders.map(o=>[o.number,o.customer,o.recipient,o.deliveryDate,o.time,this.label(o.status),total(o),paid(o),balance(o)])];this.download('flore-pedidos.csv','\uFEFF'+rows.map(row=>row.map(csvCell).join(';')).join('\r\n'),'text/csv;charset=utf-8');}
 exportReport(){const r=this.report;const rows=[['Concepto','Valor COP'],['Ventas entregadas',r.revenue],['Costo de ventas',r.cost],['Gastos operativos',r.expenses],['Merma',r.waste],['Costo cancelaciones',r.canceledCost],['Utilidad estimada',r.profit],['Cobros recibidos',r.collected]];this.download('flore-informe-'+this.reportFrom+'.csv','\uFEFF'+rows.map(row=>row.map(csvCell).join(';')).join('\r\n'),'text/csv;charset=utf-8');}
 exportData(){this.download('flore-demo-'+this.today+'.json',JSON.stringify({exportedAt:new Date().toISOString(),note:'Demo local; NO incluye fotografías.',data:this.state()},null,2),'application/json');this.notify('Registros exportados. Las fotografías no están incluidas.');}
 print(){window.print();}
 async reset(){if(!confirm('¿Reiniciar la demo? Se eliminarán los pedidos, cambios y fotografías locales. Exporta antes si deseas conservar los registros.'))return;try{await clearPhotos();this.store.reset();this.notify('Demo reiniciada con datos ficticios.');}catch(e){this.notify((e as Error).message);}}
 saveBusiness(name:string){if(!name.trim()){this.notify('Escribe un nombre para la floristería.');return;}try{this.store.commit({...this.state(),business:name.trim().slice(0,80)});this.notify('Nombre actualizado en la demo.');}catch(e){this.notify((e as Error).message);}}
}
