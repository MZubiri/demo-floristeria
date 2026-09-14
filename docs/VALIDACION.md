# Validación de la demo

## Automatizada

El workflow `Validate Angular demo` ejecuta pruebas del dominio, compilación Angular con plantillas estrictas y pruebas E2E en Chromium escritorio y WebKit con perfil iPhone 13. Las capturas están en el artefacto `flore-browser-tests`.

Las pruebas comprueban:
- No hay errores JavaScript al abrir las vistas.
- El documento no excede horizontalmente el viewport.
- Alta de pedido, destinatario distinto, dedicatoria y abonos.
- Persistencia al recargar.
- Foto local almacenada en IndexedDB y reapertura.
- Merma, gastos, exportación CSV y transiciones del pedido.
- Validación de stock, reservas, doble consumo, abonos excesivos y cancelaciones.

## Manual en iPhone físico (pendiente de realizar por el usuario)

1. Abrir por HTTPS en Safari con una versión iOS compatible con Angular 21.
2. Crear un pedido con teclado de teléfono y fecha/hora. Verificar que el teclado no tape la acción.
3. Abrir Fotografías → Tomar foto. Autorizar la cámara y tomar una foto vertical.
4. Repetir con una imagen horizontal, PNG de captura y HEIC de la galería.
5. Confirmar orientación, legibilidad del arreglo, vista previa y recarga.
6. En un navegador sin decodificación HEIC, confirmar el mensaje de error y probar JPEG.
7. Probar selección de cinco fotos: se guardan cuatro y se avisa el límite.
8. Probar un archivo >20 MB, un archivo corrupto y un SVG: deben rechazarse.
9. Cerrar Safari y abrir otra vez. Los datos locales deben seguir disponibles si no fueron borrados por el usuario o el sistema.
10. Probar conexión lenta y pérdida de conexión: la demo NO promete carga offline ni sincronización.
11. Guardar PDF desde la vista Informes y abrir CSV en Excel.
12. No utilizar datos reales: el almacenamiento local carece de autenticación.

## Recorrido para presentar (5–8 minutos)

- Inicio → indicadores y entregas del día.
- Pedidos → crear un arreglo para un destinatario distinto al comprador.
- Añadir materiales y tarjeta; guardar.
- Ficha → Fotografías → cámara o galería.
- Ficha → Pagos → registrar un abono parcial.
- Confirmar pedido y pasar a preparación; revisar reservas/consumo en Inventario.
- Registrar merma y gasto, revisar Informes.
- Mostrar vista móvil y calendario.
- Aclarar que API .NET, MySQL, archivos privados, login, auditoría protegida y respaldos son la fase de producción.

## Alcance y decisiones

Identidad ficticia Floré. Datos en COP. Fechas de prueba relativas al día local para mantener vigente la demo. Los nombres y teléfonos son ficticios.

Sin service worker, sin autenticación simulada ni pantallas de sincronización falsa. El manifiesto permite un acceso directo donde el navegador lo admite; no garantiza instalación completa en todas las plataformas. Los SVG del catálogo son ilustraciones, no fotos reales.

Los pedidos entregados o cancelados quedan cerrados. Después del consumo de materiales, solo se permite editar datos no relacionados con receta/precio del arreglo. Una cancelación con pagos se bloquea porque los reembolsos no están implementados. Una cancelación sin pagos tras preparar mantiene el consumo y reconoce su costo como pérdida de cancelación.

No hay edición de catálogos maestros ni alta independiente de proveedores, cuentas por pagar, control por lotes/caducidad, adjuntos de gastos, devoluciones o permisos por usuario. Las vistas son una muestra funcional para acordar el producto, no la totalidad del contrato inicial.

No afirmar seguridad de producción ni fiabilidad porcentual a partir de esta demo.
