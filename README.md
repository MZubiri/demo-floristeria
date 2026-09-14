# Floré · Demo de gestión de floristería

Demo interactiva en Angular, diseñada para escritorio y Safari en iPhone. Identidad y datos ficticios. Preparada para presentar al cliente y validar flujos antes de construir la API ASP.NET Core y MySQL.

> **No usar con información real.** No hay autenticación, servidor, sincronización, cobros ni copias de seguridad automáticas. Los cambios se conservan únicamente en el navegador. Las fotos seleccionadas no se suben a servicios externos.

## Ejecutar

Requiere Node.js 22.12+ (rama 22) o Node.js 24.

```sh
npm install
npm start
```

Abrir http://localhost:4200. Para probar con un iPhone en la misma red: `npm start -- --host 0.0.0.0`, acceder a la IP local del computador. Para una prueba pública, usar HTTPS en Vercel o Coolify.

## Compilar y probar

```sh
npm run build
npm run test:unit
npx playwright install --with-deps chromium webkit
npm run test:e2e
```

La compilación queda en `dist/flore/browser`. El workflow de GitHub Actions compila, ejecuta pruebas de negocio y pruebas de navegador Chromium y WebKit con tamaño iPhone. Las capturas quedan como artefactos; WebKit automatizado no reemplaza la prueba con un iPhone físico.

## Despliegue

**Vercel:** importar este repositorio. Framework Angular, comando `npm run build`, directorio `dist/flore/browser`. La configuración está en `vercel.json`.

**Coolify:** crear aplicación desde este repositorio, método Dockerfile, puerto interno 80. HTTPS lo gestiona el proxy de Coolify. No hay base de datos que configurar para esta demo.

**GitHub no publica automáticamente un sitio web al subir el código.** El workflow incluido valida el código; no activa Pages ni modifica infraestructuras externas.

## Funcionalidades

- Inicio: indicadores, gráfico semanal, próximas entregas y alertas de existencias.
- Pedidos: búsqueda, filtros, alta/edición, comprador y destinatario separados, varios productos, personalización, tarjeta, agenda y domicilio.
- Estados de preparación independientes del saldo; historial y confirmación de entrega.
- Abonos múltiples con método y saldo calculado; sin integración bancaria real.
- Catálogo con recetas y costos guardados por pedido.
- Inventario: existencias, reservas, consumo al preparar, entradas y merma con motivo.
- Gastos; clientes y proveedores; reportes por fechas.
- Exportación CSV compatible con Excel y vista de impresión para guardar PDF.
- Fotos de referencia, arreglo final y entrega: cámara/galería, orientación del navegador, redimensionado y vista previa local con IndexedDB.
- Ajustes y reinicio de datos ficticios con confirmación.
- Enlaces con fragmentos (#pedidos), navegación móvil y controles accesibles.

## Fotos y limitaciones

La demo convierte a JPEG los archivos que el navegador puede decodificar, máximo 1600 px en el lado mayor, con límite de 20 MB/archivo, 50 MP y 4 fotos por pedido. HEIC/HEIF depende de la capacidad real del navegador: si no se puede abrir, se muestra una explicación para usar JPEG. **No se simula una conversión HEIC en servidor que todavía no existe.** No admite RAW ni video.

Las imágenes se almacenan como Blob en IndexedDB; la exportación JSON incluye los registros pero **no las fotografías**. Borrar datos del sitio, cambiar de dispositivo o perder el equipo puede perder toda la demo. No hay subida en segundo plano.

Las imágenes del catálogo son ilustraciones SVG originales incluidas en el repositorio. La galería utiliza únicamente archivos elegidos por el usuario. No hay fuentes, analítica ni fotografías remotas.

## Arquitectura de producción propuesta

Frontend Angular → API HTTPS ASP.NET Core → MySQL.
Archivos privados en almacenamiento de objetos o volumen persistente, con generación de miniaturas, validación del contenido, conversión HEIC en Linux probada, límites, limpieza de EXIF y control de acceso. Respaldos de archivos y base de datos fuera del VPS. Autenticación y autorización en servidor, idempotencia, transacciones de inventario y auditoría.

Esta demo no es una implementación de contabilidad fiscal, facturación DIAN, nómina, pasarela de pago, WhatsApp API, multitienda ni sincronización offline.

## Datos y reportes

Moneda COP. Las ventas del ejemplo se reconocen al entregar; pedidos abiertos son compromisos, no ingresos realizados. Cobros se calculan por fecha de pago. La utilidad operativa estimada descuenta costo histórico de pedidos entregados, gastos operativos y mermas. Las compras de inventario son entradas valorizadas, no se descuentan nuevamente como gasto operativo. Es un modelo de demostración, no asesoría contable.

## Stack

Angular 21 (standalone, signals y formularios), TypeScript 5.9, CSS sin framework visual, IndexedDB para fotos. Lógica del dominio separada y testeable. Véase `docs/VALIDACION.md` para pruebas manuales y límites.
