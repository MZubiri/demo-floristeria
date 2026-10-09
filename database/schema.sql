-- =============================================================================
-- BASE DE DATOS: FLORÉ ESTUDIO FLORAL (MySQL 8.0+)
-- Arquitectura de Tablas y Relaciones
-- =============================================================================

CREATE DATABASE IF NOT EXISTS `gestion_floreria`
  DEFAULT CHARACTER SET utf8mb4
  DEFAULT COLLATE utf8mb4_unicode_ci;

USE `gestion_floreria`;

-- 1. ROLES
CREATE TABLE IF NOT EXISTS `Roles` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `Name` VARCHAR(50) NOT NULL,
  `Description` VARCHAR(255) NULL,
  `PermissionsJson` TEXT NULL,
  `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. USUARIOS / COLABORADORES
CREATE TABLE IF NOT EXISTS `Users` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `Name` VARCHAR(100) NOT NULL,
  `Email` VARCHAR(150) NOT NULL,
  `PasswordHash` VARCHAR(255) NOT NULL,
  `Phone` VARCHAR(30) NULL,
  `RoleId` INT NOT NULL,
  `IsActive` TINYINT(1) NOT NULL DEFAULT 1,
  `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt` DATETIME NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UK_Users_Email` (`Email`),
  CONSTRAINT `FK_Users_Roles` FOREIGN KEY (`RoleId`) REFERENCES `Roles` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3. PASE DE LISTA Y ASISTENCIA DE TRABAJADORES
CREATE TABLE IF NOT EXISTS `Attendances` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `UserId` INT NOT NULL,
  `Date` VARCHAR(10) NOT NULL,         -- Formato ISO: YYYY-MM-DD
  `ClockIn` VARCHAR(10) NULL,          -- Formato: HH:mm
  `ClockOut` VARCHAR(10) NULL,         -- Formato: HH:mm
  `Status` VARCHAR(30) NOT NULL,       -- Presente, Retardo, Falta, Justificado, Permiso
  `Notes` VARCHAR(500) NULL,
  `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  INDEX `IX_Attendances_Date` (`Date`),
  CONSTRAINT `FK_Attendances_Users` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 4. MATERIALES E INVENTARIO
CREATE TABLE IF NOT EXISTS `Materials` (
  `Id` VARCHAR(50) NOT NULL,
  `Name` VARCHAR(100) NOT NULL,
  `Category` VARCHAR(50) NOT NULL,
  `Unit` VARCHAR(30) NOT NULL,
  `Stock` DECIMAL(18, 3) NOT NULL DEFAULT 0.000,
  `Minimum` DECIMAL(18, 3) NOT NULL DEFAULT 0.000,
  `Cost` DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
  `Supplier` VARCHAR(100) NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 5. CATÁLOGO DE PRODUCTOS FLORALES
CREATE TABLE IF NOT EXISTS `Products` (
  `Id` VARCHAR(50) NOT NULL,
  `Name` VARCHAR(120) NOT NULL,
  `Category` VARCHAR(50) NOT NULL,
  `Price` DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
  `Labor` DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
  `Image` VARCHAR(255) NULL,
  `Description` VARCHAR(500) NULL,
  `IsActive` TINYINT(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 6. RECETAS DE PRODUCTOS (Composición de flores/materiales)
CREATE TABLE IF NOT EXISTS `ProductRecipes` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `ProductId` VARCHAR(50) NOT NULL,
  `MaterialId` VARCHAR(50) NOT NULL,
  `Quantity` DECIMAL(18, 3) NOT NULL DEFAULT 1.000,
  `UnitCost` DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
  PRIMARY KEY (`Id`),
  CONSTRAINT `FK_ProductRecipes_Products` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_ProductRecipes_Materials` FOREIGN KEY (`MaterialId`) REFERENCES `Materials` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 7. PEDIDOS Y VENTAS
CREATE TABLE IF NOT EXISTS `Orders` (
  `Id` VARCHAR(64) NOT NULL,
  `Number` VARCHAR(30) NOT NULL,
  `CreatedAt` VARCHAR(40) NOT NULL,
  `Customer` VARCHAR(100) NOT NULL,
  `Phone` VARCHAR(30) NOT NULL,
  `Email` VARCHAR(120) NULL,
  `Recipient` VARCHAR(100) NOT NULL,
  `RecipientPhone` VARCHAR(30) NULL,
  `Address` VARCHAR(200) NULL,
  `Area` VARCHAR(50) NULL,
  `DeliveryDate` VARCHAR(30) NOT NULL,
  `Time` VARCHAR(60) NOT NULL,
  `DeliveryMethod` VARCHAR(50) NOT NULL,
  `Priority` VARCHAR(30) NOT NULL DEFAULT 'Normal',
  `Discount` DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
  `Shipping` DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
  `HasCard` TINYINT(1) NOT NULL DEFAULT 1,
  `CardMessage` TEXT NULL,
  `Notes` TEXT NULL,
  `Status` VARCHAR(30) NOT NULL DEFAULT 'recibido',
  `Consumed` TINYINT(1) NOT NULL DEFAULT 0,
  `IsDirectSale` TINYINT(1) NOT NULL DEFAULT 0, -- Identifica venta directa en mostrador / POS local
  `DeliveredAt` VARCHAR(40) NULL,
  `ReceivedBy` VARCHAR(100) NULL,
  `DeliveryNote` VARCHAR(255) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UK_Orders_Number` (`Number`),
  INDEX `IX_Orders_DeliveryDate` (`DeliveryDate`),
  INDEX `IX_Orders_Status` (`Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 8. ÍTEMS DEL PEDIDO
CREATE TABLE IF NOT EXISTS `OrderItems` (
  `Id` VARCHAR(64) NOT NULL,
  `OrderId` VARCHAR(64) NOT NULL,
  `ProductId` VARCHAR(50) NOT NULL,
  `Name` VARCHAR(120) NOT NULL,
  `Quantity` INT NOT NULL DEFAULT 1,
  `Price` DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
  `Labor` DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
  `Notes` VARCHAR(255) NULL,
  `RecipeJson` TEXT NULL,
  PRIMARY KEY (`Id`),
  CONSTRAINT `FK_OrderItems_Orders` FOREIGN KEY (`OrderId`) REFERENCES `Orders` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 9. PAGOS Y ABONOS
CREATE TABLE IF NOT EXISTS `Payments` (
  `Id` VARCHAR(64) NOT NULL,
  `OrderId` VARCHAR(64) NOT NULL,
  `Amount` DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
  `Method` VARCHAR(50) NOT NULL,
  `Reference` VARCHAR(100) NULL,
  `Date` VARCHAR(40) NOT NULL,
  PRIMARY KEY (`Id`),
  CONSTRAINT `FK_Payments_Orders` FOREIGN KEY (`OrderId`) REFERENCES `Orders` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 10. HISTORIAL DEL PEDIDO
CREATE TABLE IF NOT EXISTS `OrderHistories` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `OrderId` VARCHAR(64) NOT NULL,
  `Date` VARCHAR(40) NOT NULL,
  `Title` VARCHAR(100) NOT NULL,
  `Note` VARCHAR(500) NULL,
  PRIMARY KEY (`Id`),
  CONSTRAINT `FK_OrderHistories_Orders` FOREIGN KEY (`OrderId`) REFERENCES `Orders` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 11. MOVIMIENTOS DE INVENTARIO (Entrada, Consumo, Merma)
CREATE TABLE IF NOT EXISTS `StockMovements` (
  `Id` VARCHAR(64) NOT NULL,
  `MaterialId` VARCHAR(50) NOT NULL,
  `Type` VARCHAR(30) NOT NULL, -- entrada, consumo, merma
  `Quantity` DECIMAL(18, 3) NOT NULL,
  `Cost` DECIMAL(18, 2) NOT NULL,
  `Date` VARCHAR(40) NOT NULL,
  `Reason` VARCHAR(255) NOT NULL,
  `OrderId` VARCHAR(64) NULL,
  PRIMARY KEY (`Id`),
  CONSTRAINT `FK_StockMovements_Materials` FOREIGN KEY (`MaterialId`) REFERENCES `Materials` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 12. GASTOS OPERATIVOS
CREATE TABLE IF NOT EXISTS `Expenses` (
  `Id` VARCHAR(64) NOT NULL,
  `Category` VARCHAR(50) NOT NULL,
  `Description` VARCHAR(255) NOT NULL,
  `Amount` DECIMAL(18, 2) NOT NULL,
  `Date` VARCHAR(10) NOT NULL,
  `Method` VARCHAR(50) NOT NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_Expenses_Date` (`Date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
