import { type State, type OrderStatus, blankOrder, newLine, saveOrder, transition, offsetDay } from './domain';
export function seed():State {
 let s:State={version:1,business:'Floré Estudio Floral',orders:[],expenses:[],movements:[],materials:[
 {id:'rosa',name:'Rosa roja premium',category:'Flores',unit:'tallos',stock:420,minimum:35,cost:3000,supplier:'Cultivos La Primavera'},
 {id:'blanca',name:'Rosa blanca',category:'Flores',unit:'tallos',stock:180,minimum:20,cost:3200,supplier:'Cultivos La Primavera'},
 {id:'girasol',name:'Girasol',category:'Flores',unit:'tallos',stock:90,minimum:12,cost:4800,supplier:'Flores del Valle'},
 {id:'tulipan',name:'Tulipán rosado',category:'Flores',unit:'tallos',stock:90,minimum:16,cost:5500,supplier:'Flores del Valle'},
 {id:'euca',name:'Eucalipto fresco',category:'Follajes',unit:'ramas',stock:100,minimum:14,cost:1800,supplier:'Cultivos La Primavera'},
 {id:'papel',name:'Papel coreano',category:'Materiales',unit:'hojas',stock:55,minimum:18,cost:2800,supplier:'Detalles & Empaques'},
 {id:'cinta',name:'Cinta de satén',category:'Materiales',unit:'metros',stock:80,minimum:12,cost:1200,supplier:'Detalles & Empaques'},
 {id:'tarjeta',name:'Tarjeta de dedicatoria',category:'Materiales',unit:'unidades',stock:28,minimum:15,cost:800,supplier:'Detalles & Empaques'},
 {id:'oso',name:'Oso de peluche pequeño',category:'Complementos',unit:'unidades',stock:4,minimum:5,cost:16000,supplier:'Detalles & Empaques'}
 ],products:[]};
 const r=(materialId:string,quantity:number)=>({materialId,quantity,unitCost:s.materials.find(m=>m.id===materialId)!.cost});
 s.products=[
 {id:'p1',name:'Amor en doce rosas',category:'Ramos',price:180000,labor:18000,image:'assets/rosas.svg',description:'Doce rosas rojas, follaje fresco y una dedicatoria inolvidable.',recipe:[r('rosa',12),r('euca',2),r('papel',2),r('cinta',1),r('tarjeta',1)]},
 {id:'p2',name:'Un poquito de sol',category:'Ramos',price:145000,labor:18000,image:'assets/girasoles.svg',description:'Girasoles luminosos envueltos en papel natural.',recipe:[r('girasol',6),r('euca',2),r('papel',2),r('cinta',1),r('tarjeta',1)]},
 {id:'p3',name:'Susurro de tulipanes',category:'Premium',price:230000,labor:24000,image:'assets/tulipanes.svg',description:'Diez tulipanes rosados para decirlo todo sin palabras.',recipe:[r('tulipan',10),r('euca',2),r('papel',2),r('cinta',1),r('tarjeta',1)]},
 {id:'p4',name:'Jardín de calma',category:'Premium',price:195000,labor:22000,image:'assets/blancas.svg',description:'Rosas blancas y eucalipto, delicadeza en su forma más pura.',recipe:[r('blanca',12),r('euca',3),r('papel',2),r('cinta',1),r('tarjeta',1)]},
 {id:'p5',name:'Abrazo floral',category:'Detalles',price:210000,labor:18000,image:'assets/abrazo.svg',description:'Rosas rojas con un pequeño compañero de peluche.',recipe:[r('rosa',8),r('oso',1),r('euca',2),r('papel',2),r('cinta',1),r('tarjeta',1)]}
 ];
 const specs:[string,string,number,number,string,OrderStatus,number][]=[
 ['Andrés Martínez','Laura Gómez',0,0,'10:30','preparacion',80000],
 ['Camila Torres','Isabel Torres',1,0,'12:00','listo',157000],
 ['Daniel Ruiz','Valentina Ruiz',2,0,'14:30','confirmado',100000],
 ['Mariana López','Elena Castro',3,0,'16:00','recibido',0],
 ['Sofía Herrera','Ana Herrera',0,0,'17:30','camino',192000],
 ['Nicolás Ríos','Paula Ríos',4,1,'09:00','confirmado',100000],
 ['Juliana Vélez','Clara Vélez',3,1,'11:00','recibido',0],
 ['Samuel García','Lucía García',1,2,'15:00','confirmado',50000],
 ['Valeria Molina','Olivia Molina',0,0,'09:00','entregado',192000],
 ['Mateo Vargas','Emma Vargas',3,-1,'14:00','entregado',207000],
 ['Isabella Cruz','Sara Cruz',2,-2,'16:00','entregado',242000],
 ['David Arias','Luisa Arias',1,-3,'12:00','entregado',157000],
 ['Gabriela Pérez','Rosa Pérez',0,-4,'13:00','entregado',192000],
 ['Sebastián Díaz','Alba Díaz',3,-5,'10:00','entregado',207000],
 ['Antonella Mora','Diana Mora',0,-6,'15:00','entregado',192000]
 ];
 specs.forEach(([customer,recipient,product,offset,time,status,amount],index)=>{
  const o=blankOrder(s); o.id='demo-order-'+(index+1); o.customer=customer; o.recipient=recipient;
  o.phone='300000'+String(index+1).padStart(4,'0'); o.recipientPhone='310000'+String(index+1).padStart(4,'0');
  o.email='cliente'+(index+1)+'@example.com'; o.address='Calle de ejemplo '+(12+index)+' # 20-30'; o.area=['Chapinero','Usaquén','Teusaquillo'][index%3];
  o.deliveryDate=offsetDay(offset); o.time=time; o.items=[newLine(s.products[product],s.materials)];
  o.createdAt=new Date(offsetDay(offset-1)+'T10:00:00').toISOString();
  o.history[0].date=o.createdAt; o.cardMessage=['Gracias por florecer a mi lado. Con todo mi amor.','Que hoy te sobren motivos para sonreír.','Un pequeño detalle para alguien extraordinario.'][index%3];
  o.notes=index===0?'Papel negro, cinta vino. Llamar antes de llegar.':index===2?'Tonos rosados suaves. Entrega de cumpleaños.':'';
  o.priority=index===0?'Alta':'Normal';
  if(amount) o.payments=[{id:'seed-pay-'+index,amount,method:['Nequi','Bancolombia','Efectivo'][index%3],date:new Date(offsetDay(Math.min(offset,0))+'T08:00:00').toISOString(),reference:'Pago de ejemplo'}];
  s=saveOrder(s,o);
  const path:OrderStatus[]=['confirmado','preparacion','listo','camino','entregado'];
  for(const step of path) {
   if(status==='recibido') break;
   s=transition(s,o.id,step,recipient,'Movimiento de ejemplo',new Date(offsetDay(offset)+'T'+time+':00').toISOString());
   if(step===status) break;
  }
 });
 s.expenses=[
 {id:'e1',category:'Domicilios',description:'Entregas locales · ruta mañana',amount:24000,date:offsetDay(0),method:'Efectivo'},
 {id:'e2',category:'Publicidad',description:'Campaña de temporada',amount:45000,date:offsetDay(-2),method:'Transferencia'},
 {id:'e3',category:'Servicios',description:'Consumo de agua del taller',amount:32000,date:offsetDay(-4),method:'Bancolombia'}
 ];
 s.materials.find(m=>m.id==='blanca')!.stock=24;
 s.movements.unshift({id:'w1',materialId:'rosa',type:'merma',quantity:5,cost:3000,date:new Date(offsetDay(-1)+'T18:00:00').toISOString(),reason:'Deterioro · registro de ejemplo'});
 s.materials.find(m=>m.id==='rosa')!.stock-=5;

 s.roles=[
  {id:1,name:'Administrador',description:'Control total de módulos y configuraciones',permissions:['*']},
  {id:2,name:'Vendedor / Cajero',description:'Punto de venta directo en local, pedidos y cobros',permissions:['orders','sales','pos','clients']},
  {id:3,name:'Florista / Armador',description:'Elaboración floral, preparación y consumo de inventario',permissions:['orders','inventory','catalog','attendance']},
  {id:4,name:'Repartidor',description:'Entregas en ruta, recepción y confirmación',permissions:['deliveries','orders','attendance']}
 ];
 s.users=[
  {id:1,name:'Elena Castro (Admin)',email:'admin@floristeria.com',phone:'+57 300 123 4567',roleId:1,roleName:'Administrador',isActive:true,createdAt:offsetDay(-30)},
  {id:2,name:'Carlos Mendoza (Caja)',email:'carlos@floristeria.com',phone:'+57 311 234 5678',roleId:2,roleName:'Vendedor / Cajero',isActive:true,createdAt:offsetDay(-20)},
  {id:3,name:'Valentina Rojas (Florista)',email:'valentina@floristeria.com',phone:'+57 320 345 6789',roleId:3,roleName:'Florista / Armador',isActive:true,createdAt:offsetDay(-15)},
  {id:4,name:'Mateo Silva (Repartidor)',email:'mateo@floristeria.com',phone:'+57 315 456 7890',roleId:4,roleName:'Repartidor',isActive:true,createdAt:offsetDay(-10)}
 ];
 s.attendances=[
  {id:1,userId:1,userName:'Elena Castro (Admin)',userRole:'Administrador',date:offsetDay(0),clockIn:'07:45',status:'Presente',notes:'Apertura de taller y administración'},
  {id:2,userId:2,userName:'Carlos Mendoza (Caja)',userRole:'Vendedor / Cajero',date:offsetDay(0),clockIn:'07:55',status:'Presente',notes:'Apertura de caja y mostrador'},
  {id:3,userId:3,userName:'Valentina Rojas (Florista)',userRole:'Florista / Armador',date:offsetDay(0),clockIn:'08:00',status:'Presente',notes:'Mesa de trabajo y pedidos del día'},
  {id:4,userId:4,userName:'Mateo Silva (Repartidor)',userRole:'Repartidor',date:offsetDay(0),clockIn:'08:25',status:'Retardo',notes:'Demora por tráfico matutino'},
  {id:5,userId:1,userName:'Elena Castro (Admin)',userRole:'Administrador',date:offsetDay(-1),clockIn:'08:00',clockOut:'17:30',status:'Presente',notes:'Jornada normal'},
  {id:6,userId:2,userName:'Carlos Mendoza (Caja)',userRole:'Vendedor / Cajero',date:offsetDay(-1),clockIn:'08:00',clockOut:'17:00',status:'Presente',notes:'Jornada normal'},
  {id:7,userId:3,userName:'Valentina Rojas (Florista)',userRole:'Florista / Armador',date:offsetDay(-1),clockIn:'08:00',clockOut:'17:00',status:'Presente',notes:'Jornada normal'},
  {id:8,userId:4,userName:'Mateo Silva (Repartidor)',userRole:'Repartidor',date:offsetDay(-1),clockIn:'08:00',clockOut:'17:00',status:'Presente',notes:'Rutas completas'}
 ];

 return s;
}
