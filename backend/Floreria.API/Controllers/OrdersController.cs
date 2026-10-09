using System.Text.Json;
using Floreria.API.Data;
using Floreria.API.DTOs;
using Floreria.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Floreria.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly FloreriaDbContext _context;
    private readonly Services.IWebSyncService _webSyncService;

    public OrdersController(FloreriaDbContext context, Services.IWebSyncService webSyncService)
    {
        _context = context;
        _webSyncService = webSyncService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetAll(
        [FromQuery] string? query,
        [FromQuery] string? status,
        [FromQuery] string? date,
        [FromQuery] bool? pendingOnly)
    {
        var q = _context.Orders
            .Where(o => !o.IsDeleted)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.History)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(o => o.Status == status);

        if (!string.IsNullOrWhiteSpace(date))
            q = q.Where(o => o.DeliveryDate == date);

        var orders = await q.OrderByDescending(o => o.CreatedAt).ToListAsync();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            orders = orders.Where(o =>
                o.Number.ToLower().Contains(term) ||
                o.Customer.ToLower().Contains(term) ||
                o.Phone.Contains(term) ||
                o.Recipient.ToLower().Contains(term) ||
                o.Items.Any(i => i.Name.ToLower().Contains(term))
            ).ToList();
        }

        if (pendingOnly == true)
        {
            orders = orders.Where(o =>
                {
                    var total = o.Items.Sum(i => i.Price * i.Quantity) - o.Discount + o.Shipping;
                    var paid = o.Payments.Sum(p => p.Amount);
                    return total > paid && o.Status != "cancelado";
                }).ToList();
        }

        return Ok(orders.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> GetById(string id)
    {
        int.TryParse(id, out int intId);
        var o = await _context.Orders
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .Include(x => x.History)
            .FirstOrDefaultAsync(x => !x.IsDeleted && (x.Id == intId || x.Number == id));

        if (o == null) return NotFound(new { message = "Pedido no encontrado." });

        return Ok(MapToDto(o));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        int.TryParse(id, out int intId);
        var o = await _context.Orders.FirstOrDefaultAsync(x => x.Id == intId || x.Number == id);
        if (o == null) return NotFound(new { message = "Pedido no encontrado." });

        o.IsDeleted = true;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Pedido eliminado lógicamente con éxito." });
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create([FromBody] OrderDto dto)
    {
        var order = new Order
        {
            Number = string.IsNullOrWhiteSpace(dto.Number) ? await GenerateOrderNumberAsync("FL-") : dto.Number,
            CreatedAt = DateTime.UtcNow,
            Customer = dto.Customer.Trim(),
            Phone = dto.Phone.Trim(),
            Email = dto.Email?.Trim() ?? "",
            Recipient = dto.Recipient.Trim(),
            RecipientPhone = dto.RecipientPhone?.Trim() ?? "",
            Address = dto.Address?.Trim() ?? "",
            Area = dto.Area?.Trim() ?? "",
            DeliveryDate = dto.DeliveryDate,
            Time = dto.Time,
            DeliveryMethod = dto.DeliveryMethod,
            Priority = dto.Priority ?? "Normal",
            Discount = dto.Discount,
            Shipping = dto.Shipping,
            CardMessage = dto.CardMessage ?? "",
            Notes = dto.Notes ?? "",
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "recibido" : dto.Status,
            Consumed = false,
            IsDirectSale = false
        };

        decimal subtotal = 0;
        foreach (var item in dto.Items)
        {
            int.TryParse(item.ProductId, out int pid);
            decimal itemTotal = item.Price * item.Quantity;
            subtotal += itemTotal;

            order.Items.Add(new OrderItem
            {
                ProductId = pid,
                Name = item.Name,
                Quantity = item.Quantity,
                Price = item.Price,
                TotalPrice = itemTotal,
                Labor = item.Labor,
                Notes = item.Notes ?? "",
                RecipeJson = JsonSerializer.Serialize(item.Recipe)
            });
        }
        order.TotalAmount = Math.Max(0, subtotal - dto.Discount + dto.Shipping);

        order.History.Add(new OrderHistory
        {
            Date = DateTime.UtcNow.ToString("o"),
            Title = "Pedido creado",
            Note = "Registrado desde el sistema"
        });

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = order.Id.ToString() }, MapToDto(order));
    }

    /// <summary>
    /// Registra una venta directa en local / mostrador, consumiendo el inventario inmediatamente y agregándose a pedidos.
    /// </summary>
    [HttpPost("direct-sale")]
    public async Task<ActionResult<OrderDto>> DirectSale([FromBody] DirectSaleDto dto)
    {
        if (dto.Items == null || !dto.Items.Any())
        {
            return BadRequest(new { message = "Debe incluir al menos un producto en la venta." });
        }

        var orderNumber = await GenerateOrderNumberAsync("POS-");
        var now = DateTime.UtcNow.ToString("o");
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var nowTime = DateTime.Now.ToString("HH:mm");

        var customerName = string.IsNullOrWhiteSpace(dto.Customer) ? "Venta en Mostrador (Cliente Local)" : dto.Customer.Trim();
        var customerPhone = string.IsNullOrWhiteSpace(dto.Phone) ? "Local" : dto.Phone.Trim();

        var order = new Order
        {
            Number = orderNumber,
            CreatedAt = DateTime.UtcNow,
            Customer = customerName,
            Phone = customerPhone,
            Recipient = customerName,
            RecipientPhone = customerPhone,
            Address = "Venta en tienda física / Mostrador",
            Area = "Local",
            DeliveryDate = today,
            Time = nowTime,
            DeliveryMethod = "Venta en tienda",
            Priority = "Inmediata",
            Discount = dto.Discount,
            Shipping = 0,
            Notes = dto.Notes ?? "Venta directa de mostrador en punto de venta",
            Status = "entregado",
            Consumed = true,
            IsDirectSale = true,
            DeliveredAt = now,
            ReceivedBy = customerName,
            DeliveryNote = "Entregado directamente al cliente en mostrador"
        };

        decimal subtotal = 0;

        // Cargar productos y recetas para descontar inventario
        var productIntIds = dto.Items
            .Select(i => int.TryParse(i.ProductId, out var pid) ? pid : 0)
            .Where(p => p > 0)
            .Distinct()
            .ToList();

        var dbProducts = await _context.Products
            .Include(p => p.Recipe)
            .Where(p => productIntIds.Contains(p.Id))
            .ToListAsync();

        var materials = await _context.Materials.ToListAsync();
        var pendingStockMovements = new List<StockMovement>();

        foreach (var item in dto.Items)
        {
            decimal itemTotal = item.Price * item.Quantity;
            subtotal += itemTotal;
            int.TryParse(item.ProductId, out int pid);
            var prod = dbProducts.FirstOrDefault(p => p.Id == pid);
            var recipeIngredients = prod != null
                ? prod.Recipe.Select(r => new IngredientDto(r.MaterialId, r.Quantity, r.UnitCost)).ToList()
                : new List<IngredientDto>();

            order.Items.Add(new OrderItem
            {
                ProductId = pid,
                Name = item.Name,
                Quantity = item.Quantity,
                Price = item.Price,
                TotalPrice = itemTotal,
                Labor = prod?.Labor ?? 0,
                Notes = "Venta directa mostrador",
                RecipeJson = JsonSerializer.Serialize(recipeIngredients)
            });

            // Descontar inventario por cada receta consumida en mostrador
            if (prod != null)
            {
                foreach (var r in prod.Recipe)
                {
                    var mat = materials.FirstOrDefault(m => m.Id == r.MaterialId);
                    if (mat != null)
                    {
                        var consumeQty = r.Quantity * item.Quantity;
                        mat.Stock = Math.Max(0, mat.Stock - consumeQty);

                        pendingStockMovements.Add(new StockMovement
                        {
                            Id = Guid.NewGuid().ToString(),
                            MaterialId = mat.Id,
                            Type = "consumo",
                            Quantity = consumeQty,
                            Cost = mat.Cost,
                            Date = now,
                            Reason = $"Venta directa mostrador {orderNumber}"
                        });
                    }
                }
            }
        }

        var total = Math.Max(0, subtotal - dto.Discount);
        order.TotalAmount = total;

        // Registrar pago completo inmediato
        order.Payments.Add(new Payment
        {
            Id = Guid.NewGuid().ToString(),
            Amount = total,
            Method = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "Efectivo" : dto.PaymentMethod,
            Reference = string.IsNullOrWhiteSpace(dto.Reference) ? "Venta en mostrador" : dto.Reference,
            Date = now
        });

        // Registrar historial
        order.History.Add(new OrderHistory
        {
            Date = now,
            Title = "Venta directa en mostrador",
            Note = $"Cobrado {total:C0} COP con {dto.PaymentMethod}. Materiales consumidos de inventario."
        });

        order.History.Add(new OrderHistory
        {
            Date = now,
            Title = "Entregado en local",
            Note = "Entregado de inmediato en el mostrador"
        });

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        if (pendingStockMovements.Any())
        {
            foreach (var sm in pendingStockMovements)
            {
                sm.OrderId = order.Id;
                _context.StockMovements.Add(sm);
            }
            await _context.SaveChangesAsync();
        }

        // Revisar insumos para desactivar productos agotados si es necesario
        _ = Task.Run(async () =>
        {
            try
            {
                await _webSyncService.CheckAndSyncStockAvailabilityAsync();
            }
            catch { }
        });

        return CreatedAtAction(nameof(GetById), new { id = order.Id.ToString() }, MapToDto(order));
    }

    [HttpPost("{id}/payments")]
    public async Task<ActionResult<OrderDto>> AddPayment(string id, [FromBody] AddPaymentDto dto)
    {
        int.TryParse(id, out int intId);
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.History)
            .FirstOrDefaultAsync(o => o.Id == intId || o.Number == id);

        if (order == null) return NotFound(new { message = "Pedido no encontrado." });
        if (order.Status == "cancelado") return BadRequest(new { message = "No se pueden agregar abonos a un pedido cancelado." });

        var total = order.Items.Sum(i => i.Price * i.Quantity) - order.Discount + order.Shipping;
        var paid = order.Payments.Sum(p => p.Amount);
        var pending = total - paid;

        if (dto.Amount <= 0) return BadRequest(new { message = "El monto debe ser mayor que cero." });
        if (dto.Amount > pending) return BadRequest(new { message = "El monto supera el saldo pendiente." });

        var now = DateTime.UtcNow.ToString("o");
        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            OrderId = order.Id,
            Amount = dto.Amount,
            Method = dto.Method,
            Reference = dto.Reference ?? "",
            Date = now
        };

        order.Payments.Add(payment);
        order.History.Add(new OrderHistory
        {
            OrderId = order.Id,
            Date = now,
            Title = "Abono registrado",
            Note = $"{dto.Method} · {dto.Amount:C0} COP {(string.IsNullOrEmpty(dto.Reference) ? "" : "· Ref: " + dto.Reference)}"
        });

        await _context.SaveChangesAsync();
        return Ok(MapToDto(order));
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(string id, [FromBody] UpdateOrderStatusDto dto)
    {
        return await Transition(id, new TransitionOrderDto(dto.Status, dto.ReceivedBy, dto.Note));
    }

    [HttpPost("{id}/transition")]
    public async Task<ActionResult<OrderDto>> Transition(string id, [FromBody] TransitionOrderDto dto)
    {
        int.TryParse(id, out int intId);
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.History)
            .FirstOrDefaultAsync(o => o.Id == intId || o.Number == id);

        if (order == null) return NotFound(new { message = "Pedido no encontrado." });

        var target = dto.TargetStatus.ToLower();
        var now = DateTime.UtcNow.ToString("o");

        if (target == "preparacion" && !order.Consumed)
        {
            // Consumir materiales
            var materials = await _context.Materials.ToListAsync();
            foreach (var item in order.Items)
            {
                if (!string.IsNullOrWhiteSpace(item.RecipeJson))
                {
                    try
                    {
                        var recipe = JsonSerializer.Deserialize<List<IngredientDto>>(item.RecipeJson);
                        if (recipe != null)
                        {
                            foreach (var ing in recipe)
                            {
                                var m = materials.FirstOrDefault(x => x.Id == ing.MaterialId);
                                if (m != null)
                                {
                                    var qty = ing.Quantity * item.Quantity;
                                    m.Stock = Math.Max(0, m.Stock - qty);
                                    _context.StockMovements.Add(new StockMovement
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        MaterialId = m.Id,
                                        Type = "consumo",
                                        Quantity = qty,
                                        Cost = m.Cost,
                                        Date = now,
                                        Reason = $"Preparación pedido {order.Number}",
                                        OrderId = order.Id
                                    });
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            order.Consumed = true;
        }

        if (target == "entregado")
        {
            order.DeliveredAt = now;
            order.ReceivedBy = dto.ReceivedBy ?? order.Recipient;
            order.DeliveryNote = dto.DeliveryNote;
        }

        order.Status = target;
        order.History.Add(new OrderHistory
        {
            OrderId = order.Id,
            Date = now,
            Title = $"Estado: {target.ToUpper()}",
            Note = dto.DeliveryNote ?? $"Transición de estado a {target}"
        });

        await _context.SaveChangesAsync();

        // Notificar nuevo estado del pedido a la tienda web y revisar insumos
        _ = Task.Run(async () =>
        {
            try
            {
                await _webSyncService.PushOrderStatusAsync(order.Number, target);
                if (target == "preparacion")
                {
                    await _webSyncService.CheckAndSyncStockAvailabilityAsync();
                }
            }
            catch { }
        });

        return Ok(MapToDto(order));
    }

    public record SetFinalPhotoRequest(string PhotoUrl);

    [HttpPost("{id}/final-photo")]
    public async Task<ActionResult<OrderDto>> SetFinalPhoto(string id, [FromBody] SetFinalPhotoRequest req)
    {
        int.TryParse(id, out int intId);
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.History)
            .FirstOrDefaultAsync(o => o.Id == intId || o.Number == id);

        if (order == null) return NotFound(new { message = "Pedido no encontrado." });
        if (string.IsNullOrWhiteSpace(req.PhotoUrl)) return BadRequest(new { message = "La URL de la foto es requerida." });

        order.FinalArrangementPhotoUrl = req.PhotoUrl.Trim();
        order.History.Add(new OrderHistory
        {
            OrderId = order.Id,
            Date = DateTime.UtcNow.ToString("o"),
            Title = "Fotografía de arreglo final agregada",
            Note = "Foto real del arreglo tomada en taller y publicada en el rastreo en vivo"
        });

        await _context.SaveChangesAsync();

        // Push final photo to Web Shop order
        _ = Task.Run(async () =>
        {
            try
            {
                await _webSyncService.PushOrderFinalPhotoAsync(order.Number, order.FinalArrangementPhotoUrl);
            }
            catch { }
        });

        return Ok(MapToDto(order));
    }

    private async Task<string> GenerateOrderNumberAsync(string prefix)
    {
        var count = await _context.Orders.CountAsync();
        return $"{prefix}{1001 + count}";
    }

    private static OrderDto MapToDto(Order o)
    {
        var total = o.Items.Sum(i => i.Price * i.Quantity) - o.Discount + o.Shipping;
        var paid = o.Payments.Sum(p => p.Amount);
        var balance = Math.Max(0, total - paid);

        return new OrderDto(
            o.Id.ToString(),
            o.Number,
            o.CreatedAt.ToString("o"),
            o.Customer,
            o.Phone,
            o.Email,
            o.Recipient,
            o.RecipientPhone,
            o.Address,
            o.Area,
            o.DeliveryDate,
            o.Time,
            o.DeliveryMethod,
            o.Priority,
            o.Discount,
            o.Shipping,
            !string.IsNullOrWhiteSpace(o.CardMessage),
            o.CardMessage,
            o.Notes,
            o.Status,
            o.Consumed,
            o.IsDirectSale,
            o.DeliveredAt,
            o.ReceivedBy,
            o.DeliveryNote,
            o.FinalArrangementPhotoUrl,
            total,
            paid,
            balance,
            o.Items.Select(i => new OrderItemDto(
                i.Id.ToString(),
                i.ProductId.ToString(),
                i.Name,
                i.Quantity,
                i.Price,
                i.Labor,
                i.Notes,
                ParseRecipe(i.RecipeJson)
            )).ToList(),
            o.Payments.Select(p => new PaymentDto(
                p.Id,
                p.Amount,
                p.Method,
                p.Reference,
                p.Date
            )).ToList(),
            o.History.OrderBy(h => h.Date).Select(h => new OrderHistoryDto(
                h.Id,
                h.Date,
                h.Title,
                h.Note
            )).ToList()
        );
    }

    private static List<IngredientDto> ParseRecipe(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<IngredientDto>();
        try
        {
            return JsonSerializer.Deserialize<List<IngredientDto>>(json) ?? new List<IngredientDto>();
        }
        catch
        {
            return new List<IngredientDto>();
        }
    }
}
