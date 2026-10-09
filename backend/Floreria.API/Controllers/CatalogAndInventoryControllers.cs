using Floreria.API.Data;
using Floreria.API.DTOs;
using Floreria.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Floreria.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly FloreriaDbContext _context;

    public ProductsController(FloreriaDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetAll()
    {
        var products = await _context.Products
            .Include(p => p.Recipe)
                .ThenInclude(r => r.Material)
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Category,
                p.Price,
                p.Labor,
                p.Image,
                p.Description,
                Recipe = p.Recipe.Select(r => new
                {
                    r.MaterialId,
                    r.Quantity,
                    UnitCost = r.Material != null ? r.Material.Cost : r.UnitCost
                })
            })
            .ToListAsync();

        return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetById(string id)
    {
        var p = await _context.Products
            .Include(x => x.Recipe)
                .ThenInclude(r => r.Material)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (p == null) return NotFound(new { message = "Producto no encontrado." });

        return Ok(new
        {
            p.Id,
            p.Name,
            p.Category,
            p.Price,
            p.Labor,
            p.Image,
            p.Description,
            p.IsActive,
            Recipe = p.Recipe.Select(r => new
            {
                r.MaterialId,
                r.Quantity,
                UnitCost = r.Material != null ? r.Material.Cost : r.UnitCost
            })
        });
    }

    public record SaveProductDto(string? Id, string Name, string Category, decimal Price, decimal Labor, string? Image, string? Description, bool IsActive, List<IngredientDto>? Recipe);

    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] SaveProductDto dto)
    {
        var id = string.IsNullOrWhiteSpace(dto.Id) ? "p_" + Guid.NewGuid().ToString("N")[..8] : dto.Id;
        var prod = new Product
        {
            Id = id,
            Name = dto.Name,
            Category = dto.Category,
            Price = dto.Price,
            Labor = dto.Labor,
            Image = dto.Image ?? "assets/rosas.svg",
            Description = dto.Description ?? "",
            IsActive = dto.IsActive
        };

        if (dto.Recipe != null)
        {
            foreach (var r in dto.Recipe)
            {
                prod.Recipe.Add(new ProductRecipe
                {
                    MaterialId = r.MaterialId,
                    Quantity = r.Quantity,
                    UnitCost = r.UnitCost
                });
            }
        }

        _context.Products.Add(prod);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = prod.Id }, prod);
    }
}

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly FloreriaDbContext _context;

    public InventoryController(FloreriaDbContext context)
    {
        _context = context;
    }

    [HttpGet("materials")]
    public async Task<ActionResult<IEnumerable<MaterialDto>>> GetMaterials()
    {
        var materials = await _context.Materials.ToListAsync();

        // Calcular reservas de pedidos confirmados no consumidos
        var reservedMap = new Dictionary<string, decimal>();
        var activeOrders = await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.Status == "confirmado" && !o.Consumed)
            .ToListAsync();

        foreach (var order in activeOrders)
        {
            foreach (var item in order.Items)
            {
                if (!string.IsNullOrWhiteSpace(item.RecipeJson))
                {
                    try
                    {
                        var recipe = System.Text.Json.JsonSerializer.Deserialize<List<IngredientDto>>(item.RecipeJson);
                        if (recipe != null)
                        {
                            foreach (var r in recipe)
                            {
                                var qty = r.Quantity * item.Quantity;
                                reservedMap[r.MaterialId] = (reservedMap.TryGetValue(r.MaterialId, out var val) ? val : 0) + qty;
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        var result = materials.Select(m =>
        {
            var res = reservedMap.TryGetValue(m.Id, out var val) ? val : 0;
            return new MaterialDto(
                m.Id,
                m.Name,
                m.Category,
                m.Unit,
                m.Stock,
                m.Minimum,
                m.Cost,
                m.Supplier,
                res,
                m.Stock - res
            );
        }).ToList();

        return Ok(result);
    }

    [HttpGet("movements")]
    public async Task<ActionResult<IEnumerable<StockMovementDto>>> GetMovements()
    {
        var movements = await _context.StockMovements
            .Include(m => m.Material)
            .OrderByDescending(m => m.Date)
            .Take(100)
            .Select(m => new StockMovementDto(
                m.Id,
                m.MaterialId,
                m.Material != null ? m.Material.Name : m.MaterialId,
                m.Type,
                m.Quantity,
                m.Cost,
                m.Date,
                m.Reason,
                m.OrderId
            ))
            .ToListAsync();

        return Ok(movements);
    }

    public record StockMovementRequest(string MaterialId, string Type, decimal Quantity, decimal? Cost, string Reason);

    [HttpPost("movements")]
    public async Task<ActionResult> CreateMovement([FromBody] StockMovementRequest req)
    {
        var m = await _context.Materials.FindAsync(req.MaterialId);
        if (m == null) return NotFound(new { message = "Material no encontrado." });
        if (req.Quantity <= 0) return BadRequest(new { message = "La cantidad debe ser mayor que cero." });

        var now = DateTime.UtcNow.ToString("o");
        var unitCost = req.Cost ?? m.Cost;

        if (req.Type == "entrada")
        {
            // Promedio ponderado de costo
            m.Cost = (m.Stock * m.Cost + req.Quantity * unitCost) / (m.Stock + req.Quantity);
            m.Stock += req.Quantity;
        }
        else if (req.Type == "merma")
        {
            m.Stock = Math.Max(0, m.Stock - req.Quantity);
        }

        var movement = new StockMovement
        {
            Id = Guid.NewGuid().ToString(),
            MaterialId = m.Id,
            Type = req.Type,
            Quantity = req.Quantity,
            Cost = unitCost,
            Date = now,
            Reason = req.Reason.Trim()
        };

        _context.StockMovements.Add(movement);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Movimiento registrado con éxito." });
    }
}

[ApiController]
[Route("api/[controller]")]
public class ExpensesController : ControllerBase
{
    private readonly FloreriaDbContext _context;

    public ExpensesController(FloreriaDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExpenseDto>>> GetAll(
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        var q = _context.Expenses.AsQueryable();

        if (!string.IsNullOrWhiteSpace(from))
            q = q.Where(e => e.Date.CompareTo(from) >= 0);

        if (!string.IsNullOrWhiteSpace(to))
            q = q.Where(e => e.Date.CompareTo(to) <= 0);

        var expenses = await q.OrderByDescending(e => e.Date)
            .Select(e => new ExpenseDto(e.Id, e.Category, e.Description, e.Amount, e.Date, e.Method))
            .ToListAsync();

        return Ok(expenses);
    }

    public record CreateExpenseRequest(string Category, string Description, decimal Amount, string Date, string Method);

    [HttpPost]
    public async Task<ActionResult<ExpenseDto>> Create([FromBody] CreateExpenseRequest req)
    {
        if (req.Amount <= 0) return BadRequest(new { message = "El monto debe ser mayor que cero." });

        var exp = new Expense
        {
            Id = Guid.NewGuid().ToString(),
            Category = req.Category.Trim(),
            Description = req.Description.Trim(),
            Amount = req.Amount,
            Date = req.Date,
            Method = req.Method
        };

        _context.Expenses.Add(exp);
        await _context.SaveChangesAsync();

        return Ok(new ExpenseDto(exp.Id, exp.Category, exp.Description, exp.Amount, exp.Date, exp.Method));
    }
}
