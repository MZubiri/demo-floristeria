import { Component, EventEmitter, Input, OnInit, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogComponent } from './dialog.component';
import { IconComponent } from './icon.component';
import { type State, type Order, type Line, newLine, total, orderCost, uid, validateOrder } from './domain';
@Component({selector:'app-order-form',standalone:true,imports:[FormsModule,DialogComponent,IconComponent],templateUrl:'./order-form.component.html'})
export class OrderFormComponent implements OnInit {
 @Input({required:true}) state!:State; @Input({required:true}) order!:Order; @Input() editing=false; @Input() serverError='';
 @Output() save=new EventEmitter<Order>(); @Output() dismiss=new EventEmitter<void>();
 draft!:Order;step=0;error=signal('');productId='';recipeOpen='';total=total;orderCost=orderCost;private initial='';
 ngOnInit(){this.draft=structuredClone(this.order);this.initial=JSON.stringify(this.draft);this.productId=this.state.products[0]?.id||'';}
 money(v:number){return new Intl.NumberFormat('es-CO',{style:'currency',currency:'COP',maximumFractionDigits:0}).format(v);}
 material(id:string){return this.state.materials.find(m=>m.id===id)?.name||id;}
 add(){const p=this.state.products.find(p=>p.id===this.productId);if(p)this.draft.items.push(newLine(p,this.state.materials));}
 custom(){this.draft.items.push({id:uid(),productId:'custom',name:'Arreglo personalizado',quantity:1,price:150000,labor:20000,recipe:[],notes:''});}
 addIngredient(line:Line,id:string){
  const m=this.state.materials.find(m=>m.id===id);if(!m||line.recipe.some(r=>r.materialId===id))return;
  line.recipe.push({materialId:id,quantity:1,unitCost:m.cost});
 }
 next(){
  this.error.set('');
  if(this.step===0 && (!this.draft.customer.trim()||!this.draft.phone.trim()||!this.draft.recipient.trim())) {this.error.set('Completa cliente, teléfono y destinatario para continuar.');return;}
  if(this.step===1 && !this.draft.items.length){this.error.set('Agrega al menos un arreglo al pedido.');return;}
  this.step=Math.min(2,this.step+1);
 }
 submit(){
  try{validateOrder(this.draft);this.error.set('');this.save.emit(structuredClone(this.draft));}catch(e){this.error.set((e as Error).message);}
 }
 close(){if(JSON.stringify(this.draft)!==this.initial && !confirm('¿Cerrar sin guardar los cambios de este pedido?'))return;this.dismiss.emit();}
}
