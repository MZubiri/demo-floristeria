-- =============================================================================
-- DATOS INICIALES: FLORÉ ESTUDIO FLORAL (MySQL 8.0+)
-- =============================================================================

USE `gestion_floreria`;

-- 1. ROLES
INSERT INTO `Roles` (`Id`, `Name`, `Description`, `PermissionsJson`) VALUES
(1, 'Administrador', 'Control total de la floristería, reportes y configuración', '["*"]'),
(2, 'Vendedor / Cajero', 'Punto de venta directo en local, pedidos y gestión de cobros', '["orders","sales","pos","clients"]'),
(3, 'Florista / Armador', 'Elaboración floral, preparación y consumo de inventario', '["orders","inventory","catalog","attendance"]'),
(4, 'Repartidor', 'Rutas de entrega, recepción y confirmación de pedidos', '["deliveries","orders","attendance"]')
ON DUPLICATE KEY UPDATE `Name`=`Name`;

-- 2. USUARIOS (Password por defecto: admin123 para admin, florer123 para los demás)
-- Hashes BCrypt válidos
INSERT INTO `Users` (`Id`, `Name`, `Email`, `PasswordHash`, `Phone`, `RoleId`, `IsActive`) VALUES
(1, 'Elena Castro (Admin)', 'admin@floristeria.com', '$2a$11$N.ZpP3b/V8n8oKsmb17pS.bJ7qA6R8U5T0nZ.N1l8j.R1fE0YmEee', '+57 300 123 4567', 1, 1),
(2, 'Carlos Mendoza (Cajero)', 'carlos@floristeria.com', '$2a$11$N.ZpP3b/V8n8oKsmb17pS.bJ7qA6R8U5T0nZ.N1l8j.R1fE0YmEee', '+57 311 234 5678', 2, 1),
(3, 'Valentina Rojas (Florista)', 'valentina@floristeria.com', '$2a$11$N.ZpP3b/V8n8oKsmb17pS.bJ7qA6R8U5T0nZ.N1l8j.R1fE0YmEee', '+57 320 345 6789', 3, 1),
(4, 'Mateo Silva (Repartidor)', 'mateo@floristeria.com', '$2a$11$N.ZpP3b/V8n8oKsmb17pS.bJ7qA6R8U5T0nZ.N1l8j.R1fE0YmEee', '+57 315 456 7890', 4, 1)
ON DUPLICATE KEY UPDATE `Name`=`Name`;

-- 3. MATERIALES
INSERT INTO `Materials` (`Id`, `Name`, `Category`, `Unit`, `Stock`, `Minimum`, `Cost`, `Supplier`) VALUES
('rosa', 'Rosa roja premium', 'Flores', 'tallos', 420.000, 35.000, 3000.00, 'Cultivos La Primavera'),
('blanca', 'Rosa blanca', 'Flores', 'tallos', 180.000, 20.000, 3200.00, 'Cultivos La Primavera'),
('girasol', 'Girasol', 'Flores', 'tallos', 90.000, 12.000, 4800.00, 'Flores del Valle'),
('tulipan', 'Tulipán rosado', 'Flores', 'tallos', 90.000, 16.000, 5500.00, 'Flores del Valle'),
('euca', 'Eucalipto fresco', 'Follajes', 'ramas', 100.000, 14.000, 1800.00, 'Cultivos La Primavera'),
('papel', 'Papel coreano', 'Materiales', 'hojas', 55.000, 18.000, 2800.00, 'Detalles & Empaques'),
('cinta', 'Cinta de satén', 'Materiales', 'metros', 80.000, 12.000, 1200.00, 'Detalles & Empaques'),
('tarjeta', 'Tarjeta de dedicatoria', 'Materiales', 'unidades', 28.000, 15.000, 800.00, 'Detalles & Empaques'),
('oso', 'Oso de peluche pequeño', 'Complementos', 'unidades', 4.000, 5.000, 16000.00, 'Detalles & Empaques')
ON DUPLICATE KEY UPDATE `Name`=`Name`;

-- 4. PRODUCTOS
INSERT INTO `Products` (`Id`, `Name`, `Category`, `Price`, `Labor`, `Image`, `Description`, `IsActive`) VALUES
('p1', 'Amor en doce rosas', 'Ramos', 180000.00, 18000.00, 'assets/rosas.svg', 'Doce rosas rojas, follaje fresco y una dedicatoria inolvidable.', 1),
('p2', 'Un poquito de sol', 'Ramos', 145000.00, 18000.00, 'assets/girasoles.svg', 'Girasoles luminosos envueltos en papel natural.', 1),
('p3', 'Susurro de tulipanes', 'Premium', 230000.00, 24000.00, 'assets/tulipanes.svg', 'Diez tulipanes rosados para decirlo todo sin palabras.', 1),
('p4', 'Jardín de calma', 'Premium', 195000.00, 22000.00, 'assets/blancas.svg', 'Rosas blancas y eucalipto, delicadeza en su forma más pura.', 1),
('p5', 'Abrazo floral', 'Detalles', 210000.00, 18000.00, 'assets/abrazo.svg', 'Rosas rojas con un pequeño compañero de peluche.', 1)
ON DUPLICATE KEY UPDATE `Name`=`Name`;

-- 5. RECETAS
INSERT INTO `ProductRecipes` (`ProductId`, `MaterialId`, `Quantity`, `UnitCost`) VALUES
('p1', 'rosa', 12.000, 3000.00),
('p1', 'euca', 2.000, 1800.00),
('p1', 'papel', 2.000, 2800.00),
('p1', 'cinta', 1.000, 1200.00),
('p1', 'tarjeta', 1.000, 800.00),

('p2', 'girasol', 6.000, 4800.00),
('p2', 'euca', 2.000, 1800.00),
('p2', 'papel', 2.000, 2800.00),
('p2', 'cinta', 1.000, 1200.00),
('p2', 'tarjeta', 1.000, 800.00),

('p3', 'tulipan', 10.000, 5500.00),
('p3', 'euca', 2.000, 1800.00),
('p3', 'papel', 2.000, 2800.00),
('p3', 'cinta', 1.000, 1200.00),
('p3', 'tarjeta', 1.000, 800.00),

('p4', 'blanca', 12.000, 3200.00),
('p4', 'euca', 3.000, 1800.00),
('p4', 'papel', 2.000, 2800.00),
('p4', 'cinta', 1.000, 1200.00),
('p4', 'tarjeta', 1.000, 800.00),

('p5', 'rosa', 8.000, 3000.00),
('p5', 'oso', 1.000, 16000.00),
('p5', 'euca', 2.000, 1800.00),
('p5', 'papel', 2.000, 2800.00),
('p5', 'cinta', 1.000, 1200.00),
('p5', 'tarjeta', 1.000, 800.00);

-- 6. ASISTENCIA INICIAL / PASE DE LISTA
INSERT INTO `Attendances` (`UserId`, `Date`, `ClockIn`, `ClockOut`, `Status`, `Notes`) VALUES
(1, CURDATE(), '07:45', NULL, 'Presente', 'Apertura de taller'),
(2, CURDATE(), '07:55', NULL, 'Presente', 'Apertura de caja'),
(3, CURDATE(), '08:00', NULL, 'Presente', 'En mesa de armado'),
(4, CURDATE(), '08:20', NULL, 'Retardo', 'Tráfico en ruta norte');

-- 7. GASTOS DE EJEMPLO
INSERT INTO `Expenses` (`Id`, `Category`, `Description`, `Amount`, `Date`, `Method`) VALUES
('e1', 'Domicilios', 'Entregas locales · ruta mañana', 24000.00, CURDATE(), 'Efectivo'),
('e2', 'Publicidad', 'Campaña de temporada redes sociales', 45000.00, CURDATE(), 'Transferencia'),
('e3', 'Servicios', 'Consumo de agua y mantenimiento taller', 32000.00, CURDATE(), 'Bancolombia');
