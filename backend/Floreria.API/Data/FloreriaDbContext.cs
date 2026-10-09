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
            entity.ToTable("Roles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(255);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
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
            entity.ToTable("Attendances");
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

        // Materials & Products
        modelBuilder.Entity<Material>(entity =>
        {
            entity.ToTable("Materials");
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
            entity.ToTable("Products");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasColumnName("NameEs").HasMaxLength(150).IsRequired();
            entity.Property(e => e.NameEn).HasColumnName("NameEn").HasMaxLength(150);
            entity.Property(e => e.Category).HasColumnName("Category").HasMaxLength(50);
            entity.Property(e => e.Price).HasColumnName("Price").HasPrecision(18, 2);
            entity.Property(e => e.Labor).HasColumnName("Labor").HasPrecision(18, 2);
            entity.Property(e => e.Image).HasColumnName("Image").HasMaxLength(300);
            entity.Property(e => e.Description).HasColumnName("DescriptionEs").HasMaxLength(500);
            entity.Property(e => e.DescriptionEn).HasColumnName("DescriptionEn").HasMaxLength(500);
            entity.Property(e => e.Featured).HasColumnName("Featured");
            entity.Property(e => e.IsActive).HasColumnName("IsActive");
            entity.Property(e => e.OccasionEs).HasColumnName("OccasionEs");
            entity.Property(e => e.OccasionEn).HasColumnName("OccasionEn");
            entity.Property(e => e.Sku).HasColumnName("Sku").HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
        });

        modelBuilder.Entity<ProductRecipe>(entity =>
        {
            entity.ToTable("ProductRecipes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.ProductId).HasColumnName("ProductId");
            entity.Property(e => e.MaterialId).HasColumnName("MaterialId").HasMaxLength(50);
            entity.Property(e => e.Quantity).HasColumnName("Quantity").HasPrecision(18, 3);
            entity.Property(e => e.UnitCost).HasColumnName("UnitCost").HasPrecision(18, 2);

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
            entity.ToTable("Orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Number).HasColumnName("OrderCode").HasMaxLength(30).IsRequired();
            entity.HasIndex(e => e.Number).IsUnique();
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.Customer).HasColumnName("CustomerName").HasMaxLength(100);
            entity.Property(e => e.Phone).HasColumnName("CustomerPhone").HasMaxLength(50);
            entity.Property(e => e.Email).HasColumnName("CustomerEmail").HasMaxLength(100);
            entity.Property(e => e.Recipient).HasColumnName("RecipientName").HasMaxLength(100);
            entity.Property(e => e.RecipientPhone).HasColumnName("RecipientPhone").HasMaxLength(50);
            entity.Property(e => e.Address).HasColumnName("DeliveryAddress").HasMaxLength(300);
            entity.Property(e => e.Area).HasColumnName("DeliveryMunicipality").HasMaxLength(100);
            entity.Property(e => e.DeliveryDate).HasColumnName("DeliveryDate").HasMaxLength(50);
            entity.Property(e => e.Time).HasColumnName("DeliveryTime").HasMaxLength(50);
            entity.Property(e => e.DeliveryMethod).HasColumnName("DeliveryMethod").HasMaxLength(50);
            entity.Property(e => e.Priority).HasColumnName("Priority").HasMaxLength(30);
            entity.Property(e => e.Discount).HasColumnName("Discount").HasPrecision(18, 2);
            entity.Property(e => e.Shipping).HasColumnName("DeliveryFee").HasPrecision(18, 2);
            entity.Property(e => e.CardStyle).HasColumnName("CardStyle").HasMaxLength(50);
            entity.Property(e => e.CardSender).HasColumnName("CardSender").HasMaxLength(100);
            entity.Property(e => e.CardMessage).HasColumnName("CardMessage").HasMaxLength(500);
            entity.Property(e => e.Notes).HasColumnName("SpecialNotes").HasMaxLength(500);
            entity.Property(e => e.TotalAmount).HasColumnName("TotalAmount").HasPrecision(18, 2);
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(30);
            entity.Property(e => e.Consumed).HasColumnName("Consumed");
            entity.Property(e => e.IsDirectSale).HasColumnName("IsDirectSale");
            entity.Property(e => e.DeliveredAt).HasColumnName("DeliveredAt").HasMaxLength(40);
            entity.Property(e => e.ReceivedBy).HasColumnName("ReceivedBy").HasMaxLength(100);
            entity.Property(e => e.DeliveryNote).HasColumnName("DeliveryNote").HasMaxLength(255);
            entity.Property(e => e.FinalArrangementPhotoUrl).HasColumnName("FinalArrangementPhotoUrl").HasMaxLength(500);

            entity.Ignore(e => e.HasCard);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.OrderId).HasColumnName("OrderId");
            entity.Property(e => e.ProductId).HasColumnName("ProductId");
            entity.Property(e => e.Name).HasColumnName("ProductName").HasMaxLength(150);
            entity.Property(e => e.Quantity).HasColumnName("Quantity");
            entity.Property(e => e.Price).HasColumnName("UnitPrice").HasPrecision(18, 2);
            entity.Property(e => e.TotalPrice).HasColumnName("TotalPrice").HasPrecision(18, 2);
            entity.Property(e => e.Labor).HasColumnName("Labor").HasPrecision(18, 2);
            entity.Property(e => e.Notes).HasColumnName("Notes").HasMaxLength(255);
            entity.Property(e => e.RecipeJson).HasColumnName("RecipeJson");

            entity.HasOne(e => e.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.OrderId).HasColumnName("OrderId");
            entity.Property(e => e.Amount).HasColumnName("Amount").HasPrecision(18, 2);
            entity.Property(e => e.Method).HasColumnName("Method").HasMaxLength(50);
            entity.Property(e => e.Reference).HasColumnName("Reference").HasMaxLength(100);
            entity.Property(e => e.Date).HasColumnName("Date").HasMaxLength(40);

            entity.HasOne(e => e.Order)
                .WithMany(o => o.Payments)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderHistory>(entity =>
        {
            entity.ToTable("OrderHistories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.OrderId).HasColumnName("OrderId");
            entity.Property(e => e.Date).HasColumnName("Date").HasMaxLength(40);
            entity.Property(e => e.Title).HasColumnName("Title").HasMaxLength(100);
            entity.Property(e => e.Note).HasColumnName("Note").HasMaxLength(500);

            entity.HasOne(e => e.Order)
                .WithMany(o => o.History)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Stock Movements & Expenses
        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("StockMovements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.MaterialId).HasColumnName("MaterialId").HasMaxLength(50);
            entity.Property(e => e.Type).HasColumnName("Type").HasMaxLength(30);
            entity.Property(e => e.Quantity).HasColumnName("Quantity").HasPrecision(18, 3);
            entity.Property(e => e.Cost).HasColumnName("Cost").HasPrecision(18, 2);
            entity.Property(e => e.Reason).HasColumnName("Reason").HasMaxLength(255);
            entity.Property(e => e.OrderId).HasColumnName("OrderId");

            entity.HasOne(e => e.Material)
                .WithMany()
                .HasForeignKey(e => e.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.ToTable("Expenses");
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
            entity.ToTable("CashRegisterClosures");
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
