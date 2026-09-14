import { test, expect } from '@playwright/test';
test('dashboard is rendered, no runtime errors and layout fits the screen',async({page},testInfo)=>{
 const errors:string[]=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('/');await expect(page.getByRole('heading',{name:'Qué bonito verte por aquí.'})).toBeVisible();
 await expect(page.getByText('Ventas de hoy',{exact:true})).toBeVisible();
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth+1)).toBeTruthy();
 await page.screenshot({path:testInfo.outputPath('01-inicio.png'),fullPage:true});
 expect(errors).toEqual([]);
});
test('all management views open without runtime errors',async({page},testInfo)=>{
 const errors:string[]=[];page.on('pageerror',e=>errors.push(e.message));
 for(const [hash,title] of [['pedidos','Pedidos'],['agenda','Agenda de entregas'],['catalogo','Catálogo floral'],['inventario','Inventario y merma'],['pagos','Ventas y pagos'],['gastos','Gastos del negocio'],['contactos','Clientes y proveedores'],['informes','Informes del negocio'],['ajustes','Configuración']]){
  await page.goto('/#'+hash);await expect(page.getByRole('heading',{name:title,exact:true,level:1})).toBeVisible();
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth+1),hash+' overflows').toBeTruthy();
 }
 await page.goto('/#pedidos');await page.screenshot({path:testInfo.outputPath('02-pedidos.png'),fullPage:true});
 expect(errors).toEqual([]);
});
test('create a custom order, register payment and preserve it on reload',async({page},testInfo)=>{
 await page.goto('/');await page.getByRole('button',{name:'Nuevo pedido',exact:true}).click();
 const modal=page.getByRole('dialog');await expect(modal).toBeVisible();
 await modal.getByLabel('Nombre del cliente *',{exact:true}).fill('Cliente de prueba');
 await modal.getByLabel('Teléfono del cliente *',{exact:true}).fill('3001234567');
 await modal.getByLabel('Nombre del destinatario *',{exact:true}).fill('Destinataria de prueba');
 await modal.getByLabel('Dirección de entrega *',{exact:true}).fill('Calle de prueba 123');
 await modal.getByRole('button',{name:'Continuar',exact:true}).click();
 await modal.getByRole('button',{name:'Agregar',exact:true}).click();
 await modal.getByRole('button',{name:'Continuar',exact:true}).click();
 await modal.getByLabel('Mensaje de la tarjeta',{exact:true}).fill('Gracias por hacer florecer este día.');
 await modal.getByRole('button',{name:'Guardar pedido',exact:true}).click();
 await expect(page.getByRole('dialog').getByRole('heading',{name:/Cliente de prueba/,level:2})).toBeVisible();
 await page.getByRole('dialog').getByRole('button',{name:'Pagos',exact:true}).click();
 await page.getByLabel('Valor del abono *',{exact:true}).fill('80000');
 await page.getByRole('button',{name:'Guardar abono',exact:true}).click();
 await expect(page.getByRole('dialog').getByText('$ 112.000',{exact:true})).toBeVisible();
 await page.screenshot({path:testInfo.outputPath('03-pedido-pago.png'),fullPage:true});
 await page.getByRole('button',{name:'Cerrar ventana'}).click();
 await page.goto('/#pedidos');await page.getByRole('textbox',{name:'Buscar pedidos'}).fill('Cliente de prueba');
 await expect(page.getByRole('button',{name:/Ver pedido/})).toHaveCount(1);
 await page.reload();await page.getByRole('textbox',{name:'Buscar pedidos'}).fill('Cliente de prueba');
 await expect(page.getByRole('button',{name:/Ver pedido/})).toHaveCount(1);
});
test('photo selection stores a local compressed image and survives reopening',async({page},testInfo)=>{
 await page.goto('/#pedidos');await page.getByRole('button',{name:'Ver pedido FL-1001',exact:true}).click();
 await page.getByRole('dialog').getByRole('button',{name:'Fotografías',exact:true}).click();
 const data=await page.evaluate(()=>{const c=document.createElement('canvas');c.width=900;c.height=600;const ctx=c.getContext('2d')!;ctx.fillStyle='#e8eddf';ctx.fillRect(0,0,900,600);ctx.fillStyle='#c68c9b';for(let i=0;i<5;i++){ctx.beginPath();ctx.arc(280+i*80,280+(i%2)*55,65,0,Math.PI*2);ctx.fill();}return c.toDataURL('image/png').split(',')[1];});
 const png=Buffer.from(data,'base64');
 await page.getByTestId('gallery-input').setInputFiles({name:'test-flor.png',mimeType:'image/png',buffer:png});
 await expect(page.getByRole('dialog').locator('.photo-grid img')).toHaveCount(1);
 await expect.poll(()=>page.getByRole('dialog').locator('.photo-grid img').evaluate((img:HTMLImageElement)=>img.complete&&img.naturalWidth>0)).toBe(true);
 await expect(page.getByRole('dialog').getByText('1 / 4',{exact:true})).toBeVisible();
 await page.screenshot({path:testInfo.outputPath('04-fotografias.png'),fullPage:true});
 await page.getByRole('button',{name:'Cerrar ventana'}).click();await page.reload();
 await page.getByRole('button',{name:'Ver pedido FL-1001',exact:true}).click();
 await page.getByRole('dialog').getByRole('button',{name:'Fotografías',exact:true}).click();
 await expect(page.getByRole('dialog').locator('.photo-grid img')).toHaveCount(1);
 await expect.poll(()=>page.getByRole('dialog').locator('.photo-grid img').evaluate((img:HTMLImageElement)=>img.complete&&img.naturalWidth>0)).toBe(true);
});
test('register waste and operating expense, export CSV',async({page})=>{
 await page.goto('/#inventario');await page.getByRole('button',{name:'Registrar merma',exact:true}).click();
 await page.getByLabel('Cantidad *',{exact:true}).fill('2');
 await page.getByLabel('Motivo o referencia *',{exact:true}).fill('Prueba de deterioro');
 await page.getByRole('button',{name:'Guardar movimiento',exact:true}).click();
 await expect(page.getByRole('dialog')).toHaveCount(0);
 await page.getByRole('button',{name:'Historial de movimientos',exact:true}).click();
 await expect(page.getByText('Prueba de deterioro',{exact:true})).toBeVisible();
 await page.goto('/#gastos');await page.getByRole('button',{name:'Registrar gasto',exact:true}).click();
 await page.getByLabel('Descripción *',{exact:true}).fill('Prueba transporte');
 await page.getByLabel('Valor COP *',{exact:true}).fill('25000');
 await page.getByRole('button',{name:'Guardar gasto',exact:true}).click();
 await expect(page.getByText('Prueba transporte',{exact:true})).toBeVisible();
 await page.goto('/#informes');
 const promise=page.waitForEvent('download');await page.getByRole('button',{name:'Exportar CSV',exact:true}).click();
 const download=await promise;expect(download.suggestedFilename()).toMatch(/flore-informe.*csv$/);
});
test('order progression consumes once; search and payment validation remain separate',async({page})=>{
 await page.goto('/#pedidos');
 await page.getByRole('button',{name:'Ver pedido FL-1004',exact:true}).click();
 await page.getByRole('dialog').getByRole('button',{name:'Confirmado',exact:true}).click();
 await expect(page.getByRole('dialog').getByText('Confirmado',{exact:true})).toBeVisible();
 await page.getByRole('dialog').getByRole('button',{name:'En preparación',exact:true}).click();
 await expect(page.getByRole('dialog').getByText('En preparación',{exact:true})).toBeVisible();
 await expect(page.getByRole('dialog').getByText('Sin pagar',{exact:true})).toBeVisible();
});

test('disguised non-image upload is rejected without adding a photo',async({page})=>{
 await page.goto('/#pedidos');await page.getByRole('button',{name:'Ver pedido FL-1001',exact:true}).click();
 await page.getByRole('dialog').getByRole('button',{name:'Fotografías',exact:true}).click();
 await page.getByTestId('gallery-input').setInputFiles({name:'not-really-a-photo.jpg',mimeType:'image/jpeg',buffer:Buffer.from('<html>not an image</html>')});
 await expect(page.getByRole('dialog').getByRole('alert')).toContainText('contenido del archivo');
 await expect(page.getByRole('dialog').locator('.photo-grid img')).toHaveCount(0);
});
