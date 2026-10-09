using Microsoft.EntityFrameworkCore;
using Floreria.API.Models;

namespace Floreria.API.Data;

public class FloreriaDbContext : DbContext
{
    public FloreriaDbContext(DbContextOptions<FloreriaDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Attendance> Attendances => Set<Attendance>();

    public DbSet<Material> Materials => Set<Material>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductRecipe> ProductRecipes => Set<ProductRecipe>();

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OrderHistory> OrderHistories => Set<OrderHistory>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<CashRegisterClosure> CashRegisterClosures => Set<CashRegisterClosure>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User & Role
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(255);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(150).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Phone).HasMaxLength(30);

            entity.HasOne(e => e.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Attendance
        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Date).HasMaxLength(10).IsRequired();
            entity.Property(e => e.ClockIn).HasMaxLength(10);
            entity.Property(e => e.ClockOut).HasMaxLength(10);
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Attendances)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Materials & Products
        modelBuilder.Entity<Material>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.Unit).HasMaxLength(30);
            entity.Property(e => e.Supplier).HasMaxLength(100);
            entity.Property(e => e.Stock).HasPrecision(18, 3);
            entity.Property(e => e.Minimum).HasPrecision(18, 3);
            entity.Property(e => e.Cost).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.Labor).HasPrecision(18, 2);
            entity.Property(e => e.Image).HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Sku).HasMaxLength(50);
        });

        modelBuilder.Entity<ProductRecipe>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProductId).HasMaxLength(50);
            entity.Property(e => e.MaterialId).HasMaxLength(50);
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.UnitCost).HasPrecision(18, 2);

            entity.HasOne(e => e.Product)
                .WithMany(p => p.Recipe)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Material)
                .WithMany()
                .HasForeignKey(e => e.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Orders
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Number).HasMaxLength(30).IsRequired();
            entity.HasIndex(e => e.Number).IsUnique();
            entity.Property(e => e.Customer).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.Email).HasMaxLength(120);
            entity.Property(e => e.Recipient).HasMaxLength(100);
            entity.Property(e => e.RecipientPhone).HasMaxLength(30);
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.Area).HasMaxLength(50);
            entity.Property(e => e.DeliveryDate).HasMaxLength(10);
            entity.Property(e => e.Time).HasMaxLength(10);
            entity.Property(e => e.DeliveryMethod).HasMaxLength(50);
            entity.Property(e => e.Priority).HasMaxLength(30);
            entity.Property(e => e.Discount).HasPrecision(18, 2);
            entity.Property(e => e.Shipping).HasPrecision(18, 2);
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.FinalArrangementPhotoUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.OrderId).HasMaxLength(64);
            entity.Property(e => e.ProductId).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(120);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.Labor).HasPrecision(18, 2);

            entity.HasOne(e => e.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.OrderId).HasMaxLength(64);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Method).HasMaxLength(50);
            entity.Property(e => e.Reference).HasMaxLength(100);

            entity.HasOne(e => e.Order)
                .WithMany(o => o.Payments)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderId).HasMaxLength(64);
            entity.Property(e => e.Title).HasMaxLength(100);
            entity.Property(e => e.Note).HasMaxLength(500);

            entity.HasOne(e => e.Order)
                .WithMany(o => o.History)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Stock Movements & Expenses
        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.MaterialId).HasMaxLength(50);
            entity.Property(e => e.Type).HasMaxLength(30);
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.Cost).HasPrecision(18, 2);
            entity.Property(e => e.Reason).HasMaxLength(255);
            entity.Property(e => e.OrderId).HasMaxLength(64);

            entity.HasOne(e => e.Material)
                .WithMany()
                .HasForeignKey(e => e.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Date).HasMaxLength(10);
            entity.Property(e => e.Method).HasMaxLength(50);
        });

        modelBuilder.Entity<CashRegisterClosure>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Date).HasMaxLength(10).IsRequired();
            entity.Property(e => e.ClosedAt).HasMaxLength(40).IsRequired();
            entity.Property(e => e.CashierName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.OpeningBalance).HasPrecision(18, 2);
            entity.Property(e => e.CashSales).HasPrecision(18, 2);
            entity.Property(e => e.ElectronicSales).HasPrecision(18, 2);
            entity.Property(e => e.TotalSales).HasPrecision(18, 2);
            entity.Property(e => e.TotalExpenses).HasPrecision(18, 2);
            entity.Property(e => e.ExpectedCash).HasPrecision(18, 2);
            entity.Property(e => e.ActualCash).HasPrecision(18, 2);
            entity.Property(e => e.Difference).HasPrecision(18, 2);
        });
    }
}
