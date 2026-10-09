namespace Floreria.API.Models;

public class Material
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Stock { get; set; }
    public decimal Minimum { get; set; }
    public decimal Cost { get; set; }
    public string Supplier { get; set; } = string.Empty;
}

public class Product
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Labor { get; set; }
    public string Image { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? Sku { get; set; }

    public ICollection<ProductRecipe> Recipe { get; set; } = new List<ProductRecipe>();
}

public class ProductRecipe
{
    public int Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public Product? Product { get; set; }
    public string MaterialId { get; set; } = string.Empty;
    public Material? Material { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class Order
{
    public string Id { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
    public string Customer { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string DeliveryDate { get; set; } = string.Empty; // YYYY-MM-DD
    public string Time { get; set; } = string.Empty;         // HH:mm
    public string DeliveryMethod { get; set; } = "Domicilio"; // Domicilio, Recoger en tienda, Venta local
    public string Priority { get; set; } = "Normal";
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public bool HasCard { get; set; }
    public string CardMessage { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Status { get; set; } = "recibido"; // recibido, confirmado, preparacion, listo, camino, entregado, cancelado
    public bool Consumed { get; set; }
    public bool IsDirectSale { get; set; } = false;
    public string? DeliveredAt { get; set; }
    public string? ReceivedBy { get; set; }
    public string? DeliveryNote { get; set; }
    public string? FinalArrangementPhotoUrl { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<OrderHistory> History { get; set; } = new List<OrderHistory>();
}

public class OrderItem
{
    public string Id { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public Order? Order { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Labor { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string RecipeJson { get; set; } = "[]";
}

public class Payment
{
    public string Id { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public Order? Order { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Efectivo";
    public string Reference { get; set; } = string.Empty;
    public string Date { get; set; } = DateTime.UtcNow.ToString("o");
}

public class OrderHistory
{
    public int Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public Order? Order { get; set; }
    public string Date { get; set; } = DateTime.UtcNow.ToString("o");
    public string Title { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

public class StockMovement
{
    public string Id { get; set; } = string.Empty;
    public string MaterialId { get; set; } = string.Empty;
    public Material? Material { get; set; }
    public string Type { get; set; } = "consumo"; // entrada, consumo, merma
    public decimal Quantity { get; set; }
    public decimal Cost { get; set; }
    public string Date { get; set; } = DateTime.UtcNow.ToString("o");
    public string Reason { get; set; } = string.Empty;
    public string? OrderId { get; set; }
}

public class Expense
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Date { get; set; } = string.Empty; // YYYY-MM-DD
    public string Method { get; set; } = "Efectivo";
}

public class CashRegisterClosure
{
    public string Id { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty; // YYYY-MM-DD
    public string ClosedAt { get; set; } = DateTime.UtcNow.ToString("o");
    public string CashierName { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public decimal CashSales { get; set; }
    public decimal ElectronicSales { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal ActualCash { get; set; }
    public decimal Difference { get; set; }
    public int OrderCount { get; set; }
    public string? Notes { get; set; }
}
