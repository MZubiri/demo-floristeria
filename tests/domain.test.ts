import { test } from 'node:test';
import assert from 'node:assert/strict';
import { blankOrder, saveOrder, transition, moveStock, addPayment, reserved, available, total, balance, orderCost, summary, csvCell, dayKey, type State } from '../src/app/domain.ts';
function base():State{return {version:1,business:'Test',materials:[{id:'rose',name:'Rosa',category:'Flores',unit:'tallos',stock:30,minimum:5,cost:3000,supplier:'Test'}],products:[],orders:[],expenses:[],movements:[]};}
function order(s:State){const o=blankOrder(s);Object.assign(o,{customer:'Cliente prueba',phone:'3001234567',recipient:'Destinataria',address:'Calle de ejemplo',shipping:12000});o.items=[{id:'line',productId:'rose12',name:'12 rosas',quantity:1,price:180000,labor:18000,recipe:[{materialId:'rose',quantity:12,unitCost:3000}],notes:''}];return o;}
test('total, historical cost and balance are independent of current purchase cost',()=>{
 const s=base(),o=order(s);o.discount=10000;assert.equal(total(o),182000);assert.equal(orderCost(o),54000);
 s.materials[0].cost=9000;assert.equal(orderCost(o),54000);assert.equal(balance(o),182000);
});
test('confirmation reserves and preparation consumes exactly once',()=>{
 let s=base(),o=order(s);s=saveOrder(s,o);s=transition(s,o.id,'confirmado');
 assert.equal(s.materials[0].stock,30);assert.equal(reserved(s,'rose'),12);assert.equal(available(s,'rose'),18);
 s=transition(s,o.id,'preparacion');assert.equal(s.materials[0].stock,18);assert.equal(reserved(s,'rose'),0);assert.equal(s.movements.length,1);
 s=transition(s,o.id,'preparacion');assert.equal(s.materials[0].stock,18);assert.equal(s.movements.length,1);
});
test('two confirmed orders cannot reserve the same inventory',()=>{
 let s=base();s.materials[0].stock=20;const a=order(s);s=saveOrder(s,a);s=transition(s,a.id,'confirmado');
 const b=order(s);s=saveOrder(s,b);assert.throws(()=>transition(s,b.id,'confirmado'),/inventario/);assert.equal(s.materials[0].stock,20);
});
test('cancellation releases unconsumed reservations without inventing stock',()=>{
 let s=base(),o=order(s);s=saveOrder(s,o);s=transition(s,o.id,'confirmado');s=transition(s,o.id,'cancelado');
 assert.equal(s.materials[0].stock,30);assert.equal(reserved(s,'rose'),0);
});
test('multiple payments, overpayment rejection and positive amount validation',()=>{
 let s=base(),o=order(s);s=saveOrder(s,o);s=addPayment(s,o.id,80000,'Nequi');
 assert.equal(balance(s.orders[0]),112000);s=addPayment(s,o.id,112000,'Efectivo');assert.equal(balance(s.orders[0]),0);
 assert.throws(()=>addPayment(s,o.id,1,'Nequi'),/supera/);assert.throws(()=>addPayment(s,o.id,NaN,'Nequi'));assert.throws(()=>addPayment(s,o.id,-2,'Nequi'));
});
test('orders with payments cannot silently cancel or lose money',()=>{
 let s=base(),o=order(s);s=saveOrder(s,o);s=addPayment(s,o.id,80000,'Nequi');assert.throws(()=>transition(s,o.id,'cancelado'),/abonos/);assert.equal(s.orders[0].payments.length,1);
});
test('waste decreases stock once and cannot use reserved material',()=>{
 let s=base(),o=order(s);s=saveOrder(s,o);s=transition(s,o.id,'confirmado');s=moveStock(s,'rose','merma',5,'Deterioro');
 assert.equal(s.materials[0].stock,25);assert.equal(available(s,'rose'),13);assert.throws(()=>moveStock(s,'rose','merma',14,'Deterioro'));
 assert.equal(summary(s,dayKey(),dayKey()).waste,15000);
});
test('inventory purchase updates weighted cost and is not a duplicate operating expense',()=>{
 const s=moveStock(base(),'rose','entrada',30,'Factura',5000);assert.equal(s.materials[0].cost,4000);assert.equal(s.materials[0].stock,60);assert.equal(summary(s,dayKey(),dayKey()).expenses,0);
});
test('delivery requires a recipient and can retain an unpaid balance',()=>{
 let s=base(),o=order(s);s=saveOrder(s,o);for(const status of ['confirmado','preparacion','listo','camino'] as const)s=transition(s,o.id,status);
 assert.throws(()=>transition(s,o.id,'entregado',''),/recibió/);s=transition(s,o.id,'entregado','Persona receptora');
 assert.equal(balance(s.orders[0]),192000);assert.equal(summary(s,dayKey(),dayKey()).revenue,192000);
 assert.throws(()=>saveOrder(s,s.orders[0]),/cerrado/);
});
test('negative quantities, excessive discounts, invalid transitions and invalid input are rejected',()=>{
 const s=base(),o=order(s);o.items[0].quantity=-1;assert.throws(()=>saveOrder(s,o));
 o.items[0].quantity=1;o.discount=999999;assert.throws(()=>saveOrder(s,o),/descuento/);
 o.discount=0;const next=saveOrder(s,o);assert.throws(()=>transition(next,o.id,'entregado','Test'),/Transición/);
});
test('preparation freezes recipe edits but delivery notes can still change',()=>{
 let s=base(),o=order(s);s=saveOrder(s,o);s=transition(s,o.id,'confirmado');s=transition(s,o.id,'preparacion');
 const edit=structuredClone(s.orders[0]);edit.notes='Nueva referencia';s=saveOrder(s,edit);assert.equal(s.orders[0].notes,'Nueva referencia');
 edit.items[0].quantity=2;assert.throws(()=>saveOrder(s,edit),/consumidos/);
});
test('CSV export quotes strings and neutralizes spreadsheet formulas',()=>{
 assert.equal(csvCell('=SUM(1,2)'),'"\'=SUM(1,2)"');assert.equal(csvCell('hola "flor"'),'"hola ""flor"""');
});

test('fractional payments and malformed emails are rejected',()=>{
 const s=base(),o=order(s);const withOrder=saveOrder(s,o);
 assert.throws(()=>addPayment(withOrder,o.id,0.2,'Nequi'),/enteros/);
 o.email='invalid';assert.throws(()=>saveOrder(s,o),/correo/);
 o.email='valid@example.com';assert.doesNotThrow(()=>saveOrder(s,o));
});
