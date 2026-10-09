# Floré · Sistema de Gestión de Floristería

Solución integral de gestión y ventas para floristerías:
- **Backend:** ASP.NET Core (.NET 9 Web API) con Entity Framework Core, ClosedXML, Swagger UI y autenticación JWT.
- **Frontend:** Angular 21 (Signals, componentes standalone, diseño responsive para móvil y escritorio).
- **Base de Datos:** MySQL 8.0 relacional con DDL (`database/schema.sql`) y datos semilla (`database/seed.sql`).
- **Contenedores:** Orquestación lista para producción con `docker-compose.yml`.

---

## Nuevas Funcionalidades Integradas

1. **Pase de Lista de Trabajadores (Asistencia):**
   - Módulo en el menú lateral para control de asistencias diario.
   - Registro ágil de hora de entrada (`ClockIn`), salida (`ClockOut`), y estado: **Presente**, **Retardo**, **Falta**, **Justificado** o **Permiso**.
   - Resumen métrico del día (plantilla activa, presentes, retardos, incidencias).
   - Exportación de la sábana de asistencia directamente a Excel (`.xlsx`).

2. **CRUD Completo de Usuarios y Roles:**
   - Gestión integral de colaboradores: alta, edición, activación/inactivación y eliminación.
   - Roles preconfigurados con permisos diferenciados:
     - **Administrador:** Control total del sistema y ajustes.
     - **Vendedor / Cajero:** Punto de venta directo (POS), pedidos y cobros.
     - **Florista / Armador:** Mesa de preparación, recetas y consumo de inventario.
     - **Repartidor:** Rutas de entrega, confirmación de entregas y asistencia.
   - Cifrado seguro de contraseñas con BCrypt y autenticación mediante JWT Bearer.

3. **Venta Directa en Local (Punto de Venta / Mostrador):**
   - Botón de acceso rápido **"Venta Local"** en la barra superior y en el módulo de Pedidos.
   - Registro exprés de ventas en mostrador: selección de arreglos/flores, cantidad, descuento y método de pago (Efectivo, Nequi, Tarjeta, Bancolombia, etc.).
   - Al confirmar:
     - Se descuenta automáticamente el inventario de materiales consumidos según la receta floral.
     - Se registra el cobro al 100% (o abono) de forma inmediata.
     - El pedido se agrega a la lista general con la etiqueta distintiva **"Local"**, quedando reflejado en las métricas e informes de ventas del día.

4. **Exportación de Información a Excel (.xlsx):**
   - **Pedidos y Ventas:** Exporta detalle con número, cliente, canal/origen, fecha, estado, total, abonos y saldo pendiente.
   - **Asistencia / Pase de Lista:** Exporta histórico de asistencias con horas, colaborador, cargo y observaciones.
   - **Inventario:** Exporta catálogo de materiales con stock físico, reservas activas, disponible, costo unitario, valorización total y proveedor.
   - **Estado Financiero:** Exporta ingresos, costos, gastos operativos, merma y utilidad neta.
   - Generación nativa tanto en backend con **ClosedXML** (`/api/export/...`) como en frontend compatible con Microsoft Excel.

---

## Estructura del Proyecto

```
GestionFloreria/
├── backend/
│   ├── Floreria.sln                     # Solución .NET
│   └── Floreria.API/
│       ├── Controllers/                # Controladores REST API (Auth, Users, Roles, Attendance, Orders, Inventory, Export, etc.)
│       ├── Models/                     # Modelos de dominio Entity Framework
│       ├── Data/                       # DbContext y DbInitializer (Seed)
│       ├── DTOs/                       # Data Transfer Objects
│       ├── Services/                   # ClosedXML ExcelExportService, AuthService (JWT, BCrypt)
│       ├── appsettings.json            # Cadena de conexión MySQL y configuración
│       ├── Dockerfile                  # Dockerfile del backend
│       └── Program.cs                  # Pipeline, inyección de dependencias, CORS, Swagger
├── database/
│   ├── schema.sql                      # DDL completo de MySQL 8
│   └── seed.sql                        # Datos iniciales (usuarios, roles, flores, productos, etc.)
├── src/                                # Frontend Angular (componentes, servicios, vistas)
│   ├── app/
│   │   ├── api.service.ts              # Servicio de comunicación con la API y exportación Excel
│   │   ├── domain.ts                   # Lógica de negocio (pedidos, asistencia, POS, usuarios, inventario)
│   │   ├── store.ts                    # Store reactivo con persistencia
│   │   ├── app.component.ts            # Controlador principal con nuevas vistas
│   │   ├── app.component.html          # Vistas de pedidos, pase de lista, usuarios, POS, etc.
│   │   ├── icon.component.ts           # Iconos SVG
│   │   └── seed.ts                     # Datos base de demostración
├── tests/
│   └── domain.test.ts                  # Pruebas unitarias de dominio (17/17 superadas)
├── docker-compose.yml                  # Orquestación de MySQL, Backend API y Frontend
└── README.md
```

---

## Cómo Ejecutar el Proyecto

### Opción 1: Todo en Docker (Recomendada)
Para levantar la base de datos MySQL 8, el backend ASP.NET Core y el frontend en contenedores:

```bash
docker compose up --build
```

- **Frontend Angular:** http://localhost:8080
- **Backend Swagger UI:** http://localhost:5000/swagger
- **Base de Datos MySQL:** `localhost:3306` (usuario: `root`, contraseña: `root`, base de datos: `gestion_floreria`)

---

### Opción 2: Ejecución Local

#### 1. Base de datos MySQL
Crea la base de datos ejecutando los scripts:
```bash
mysql -u root -p < database/schema.sql
mysql -u root -p < database/seed.sql
```
*(Opcionalmente, el backend creará e inicializará automáticamente las tablas en su primer inicio si MySQL está activo).*

#### 2. Backend ASP.NET Core (.NET 9)
```bash
cd backend/Floreria.API
dotnet restore
dotnet run
```
La API estará disponible en `http://localhost:5000` con documentación interactiva en:
`http://localhost:5000/swagger`

#### 3. Frontend Angular
En una terminal en la raíz del proyecto:
```bash
npm install
npm start
```
Abre en tu navegador:
`http://localhost:4200`

---

## Credenciales de Acceso por Defecto

- **Administrador:**
  - Correo: `admin@floristeria.com`
  - Contraseña: `admin123`
- **Vendedor / Cajero:**
  - Correo: `carlos@floristeria.com`
  - Contraseña: `florer123`
- **Florista:**
  - Correo: `valentina@floristeria.com`
  - Contraseña: `florer123`
- **Repartidor:**
  - Correo: `mateo@floristeria.com`
  - Contraseña: `florer123`

---

## Pruebas y Validación

- **Pruebas unitarias de frontend / lógica de dominio (17 pruebas):**
  ```bash
  npm run test:unit
  ```
- **Compilación de producción Angular:**
  ```bash
  npm run build
  ```
- **Compilación del Backend .NET:**
  ```bash
  dotnet build backend/Floreria.sln
  ```
