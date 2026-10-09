using Floreria.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Floreria.API.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(FloreriaDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        // 1. Roles
        if (!await context.Roles.AnyAsync())
        {
            var roles = new List<Role>
            {
                new() { Id = 1, Name = "Administrador", Description = "Acceso total a todos los módulos y configuraciones", PermissionsJson = "[\"*\"]" },
                new() { Id = 2, Name = "Vendedor / Cajero", Description = "Gestión de ventas en local, pedidos y cobros", PermissionsJson = "[\"orders\",\"sales\",\"pos\",\"clients\"]" },
                new() { Id = 3, Name = "Florista / Armador", Description = "Elaboración de ramos, consumo de inventario y estado de preparación", PermissionsJson = "[\"orders\",\"inventory\",\"catalog\",\"attendance\"]" },
                new() { Id = 4, Name = "Repartidor", Description = "Entregas en ruta, confirmación de entrega y asistencia", PermissionsJson = "[\"deliveries\",\"orders\",\"attendance\"]" }
            };
            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }

        // 2. Users (Seed & Ensure all roles exist)
        var defaultUsers = new (string Name, string Email, string Pass, string Phone, int RoleId)[]
        {
            ("Elena Castro (Admin)", "admin@floristeria.com", "admin123", "+57 300 123 4567", 1),
            ("Laura Gómez (Caja)", "laura@floristeria.com", "laura123", "+57 310 987 6543", 2),
            ("Carlos Mendoza (Florista)", "carlos@floristeria.com", "carlos123", "+57 311 234 5678", 3),
            ("Andrés Martínez (Repartidor)", "andres@floristeria.com", "andres123", "+57 315 456 7890", 4),
            ("Valentina Rojas (Florista)", "valentina@floristeria.com", "valentina123", "+57 320 345 6789", 3),
            ("Mateo Silva (Repartidor)", "mateo@floristeria.com", "mateo123", "+57 312 654 9870", 4)
        };

        foreach (var def in defaultUsers)
        {
            var existing = await context.Users.FirstOrDefaultAsync(u => u.Email == def.Email);
            if (existing == null)
            {
                await context.Users.AddAsync(new User
                {
                    Name = def.Name,
                    Email = def.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(def.Pass),
                    Phone = def.Phone,
                    RoleId = def.RoleId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        await context.SaveChangesAsync();

        // 3. Materials
        if (!await context.Materials.AnyAsync())
        {
            var materials = new List<Material>
            {
                new() { Id = "rosa", Name = "Rosa roja premium", Category = "Flores", Unit = "tallos", Stock = 420, Minimum = 35, Cost = 3000, Supplier = "Cultivos La Primavera" },
                new() { Id = "blanca", Name = "Rosa blanca", Category = "Flores", Unit = "tallos", Stock = 180, Minimum = 20, Cost = 3200, Supplier = "Cultivos La Primavera" },
                new() { Id = "girasol", Name = "Girasol", Category = "Flores", Unit = "tallos", Stock = 90, Minimum = 12, Cost = 4800, Supplier = "Flores del Valle" },
                new() { Id = "tulipan", Name = "Tulipán rosado", Category = "Flores", Unit = "tallos", Stock = 90, Minimum = 16, Cost = 5500, Supplier = "Flores del Valle" },
                new() { Id = "euca", Name = "Eucalipto fresco", Category = "Follajes", Unit = "ramas", Stock = 100, Minimum = 14, Cost = 1800, Supplier = "Cultivos La Primavera" },
                new() { Id = "papel", Name = "Papel coreano", Category = "Materiales", Unit = "hojas", Stock = 55, Minimum = 18, Cost = 2800, Supplier = "Detalles & Empaques" },
                new() { Id = "cinta", Name = "Cinta de satén", Category = "Materiales", Unit = "metros", Stock = 80, Minimum = 12, Cost = 1200, Supplier = "Detalles & Empaques" },
                new() { Id = "tarjeta", Name = "Tarjeta de dedicatoria", Category = "Materiales", Unit = "unidades", Stock = 28, Minimum = 15, Cost = 800, Supplier = "Detalles & Empaques" },
                new() { Id = "oso", Name = "Oso de peluche pequeño", Category = "Complementos", Unit = "unidades", Stock = 4, Minimum = 5, Cost = 16000, Supplier = "Detalles & Empaques" }
            };
            await context.Materials.AddRangeAsync(materials);
            await context.SaveChangesAsync();
        }

        // 4. Products & Recipes
        if (!await context.Products.AnyAsync())
        {
            var p1 = new Product { Id = "p1", Name = "Amor en doce rosas", Category = "Ramos", Price = 180000, Labor = 18000, Image = "assets/rosas.svg", Description = "Doce rosas rojas, follaje fresco y una dedicatoria inolvidable." };
            var p2 = new Product { Id = "p2", Name = "Un poquito de sol", Category = "Ramos", Price = 145000, Labor = 18000, Image = "assets/girasoles.svg", Description = "Girasoles luminosos envueltos en papel natural." };
            var p3 = new Product { Id = "p3", Name = "Susurro de tulipanes", Category = "Premium", Price = 230000, Labor = 24000, Image = "assets/tulipanes.svg", Description = "Diez tulipanes rosados para decirlo todo sin palabras." };
            var p4 = new Product { Id = "p4", Name = "Jardín de calma", Category = "Premium", Price = 195000, Labor = 22000, Image = "assets/blancas.svg", Description = "Rosas blancas y eucalipto, delicadeza en su forma más pura." };
            var p5 = new Product { Id = "p5", Name = "Abrazo floral", Category = "Detalles", Price = 210000, Labor = 18000, Image = "assets/abrazo.svg", Description = "Rosas rojas con un pequeño compañero de peluche." };

            await context.Products.AddRangeAsync(p1, p2, p3, p4, p5);
            await context.SaveChangesAsync();

            var recipes = new List<ProductRecipe>
            {
                new() { ProductId = "p1", MaterialId = "rosa", Quantity = 12, UnitCost = 3000 },
                new() { ProductId = "p1", MaterialId = "euca", Quantity = 2, UnitCost = 1800 },
                new() { ProductId = "p1", MaterialId = "papel", Quantity = 2, UnitCost = 2800 },
                new() { ProductId = "p1", MaterialId = "cinta", Quantity = 1, UnitCost = 1200 },
                new() { ProductId = "p1", MaterialId = "tarjeta", Quantity = 1, UnitCost = 800 },

                new() { ProductId = "p2", MaterialId = "girasol", Quantity = 6, UnitCost = 4800 },
                new() { ProductId = "p2", MaterialId = "euca", Quantity = 2, UnitCost = 1800 },
                new() { ProductId = "p2", MaterialId = "papel", Quantity = 2, UnitCost = 2800 },
                new() { ProductId = "p2", MaterialId = "cinta", Quantity = 1, UnitCost = 1200 },
                new() { ProductId = "p2", MaterialId = "tarjeta", Quantity = 1, UnitCost = 800 },

                new() { ProductId = "p3", MaterialId = "tulipan", Quantity = 10, UnitCost = 5500 },
                new() { ProductId = "p3", MaterialId = "euca", Quantity = 2, UnitCost = 1800 },
                new() { ProductId = "p3", MaterialId = "papel", Quantity = 2, UnitCost = 2800 },
                new() { ProductId = "p3", MaterialId = "cinta", Quantity = 1, UnitCost = 1200 },
                new() { ProductId = "p3", MaterialId = "tarjeta", Quantity = 1, UnitCost = 800 },

                new() { ProductId = "p4", MaterialId = "blanca", Quantity = 12, UnitCost = 3200 },
                new() { ProductId = "p4", MaterialId = "euca", Quantity = 3, UnitCost = 1800 },
                new() { ProductId = "p4", MaterialId = "papel", Quantity = 2, UnitCost = 2800 },
                new() { ProductId = "p4", MaterialId = "cinta", Quantity = 1, UnitCost = 1200 },
                new() { ProductId = "p4", MaterialId = "tarjeta", Quantity = 1, UnitCost = 800 },

                new() { ProductId = "p5", MaterialId = "rosa", Quantity = 8, UnitCost = 3000 },
                new() { ProductId = "p5", MaterialId = "oso", Quantity = 1, UnitCost = 16000 },
                new() { ProductId = "p5", MaterialId = "euca", Quantity = 2, UnitCost = 1800 },
                new() { ProductId = "p5", MaterialId = "papel", Quantity = 2, UnitCost = 2800 },
                new() { ProductId = "p5", MaterialId = "cinta", Quantity = 1, UnitCost = 1200 },
                new() { ProductId = "p5", MaterialId = "tarjeta", Quantity = 1, UnitCost = 800 }
            };
            await context.ProductRecipes.AddRangeAsync(recipes);
            await context.SaveChangesAsync();
        }

        // 5. Sample Attendance (Pase de lista de trabajadores)
        if (!await context.Attendances.AnyAsync())
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
            var users = await context.Users.ToListAsync();

            var attendances = new List<Attendance>();
            foreach (var user in users)
            {
                // Ayer
                attendances.Add(new Attendance
                {
                    UserId = user.Id,
                    Date = yesterday,
                    ClockIn = "08:00",
                    ClockOut = "17:00",
                    Status = "Presente",
                    Notes = "Jornada completa habitual"
                });

                // Hoy
                attendances.Add(new Attendance
                {
                    UserId = user.Id,
                    Date = today,
                    ClockIn = user.Id == 4 ? "08:25" : "07:55",
                    ClockOut = null,
                    Status = user.Id == 4 ? "Retardo" : "Presente",
                    Notes = user.Id == 4 ? "Demora en transporte matutino" : "Entrada registrada a tiempo"
                });
            }

            await context.Attendances.AddRangeAsync(attendances);
            await context.SaveChangesAsync();
        }

        // 6. Sample Expenses
        if (!await context.Expenses.AnyAsync())
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var expenses = new List<Expense>
            {
                new() { Id = "e1", Category = "Domicilios", Description = "Entregas locales · ruta mañana", Amount = 24000, Date = today, Method = "Efectivo" },
                new() { Id = "e2", Category = "Publicidad", Description = "Campaña de temporada redes sociales", Amount = 45000, Date = today, Method = "Transferencia" },
                new() { Id = "e3", Category = "Servicios", Description = "Consumo de agua y mantenimiento taller", Amount = 32000, Date = today, Method = "Bancolombia" }
            };
            await context.Expenses.AddRangeAsync(expenses);
            await context.SaveChangesAsync();
        }
    }
}
