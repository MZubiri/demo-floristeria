using Floreria.API.Data;
using Floreria.API.DTOs;
using Floreria.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Floreria.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly FloreriaDbContext _context;

    public ReportsController(FloreriaDbContext context)
    {
        _context = context;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<FinancialSummaryDto>> GetSummary(
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        var fromDate = string.IsNullOrWhiteSpace(from) ? DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd") : from;
        var toDate = string.IsNullOrWhiteSpace(to) ? DateTime.UtcNow.ToString("yyyy-MM-dd") : to;

        var completedOrders = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Where(o => o.Status == "entregado" && o.DeliveryDate.CompareTo(fromDate) >= 0 && o.DeliveryDate.CompareTo(toDate) <= 0)
            .ToListAsync();

        decimal revenue = completedOrders.Sum(o => o.Items.Sum(i => i.Price * i.Quantity) - o.Discount + o.Shipping);
        decimal cost = completedOrders.Sum(o => o.Items.Sum(i => i.Labor * i.Quantity)); // Direct labor & base costs

        var expenses = await _context.Expenses
            .Where(e => e.Date.CompareTo(fromDate) >= 0 && e.Date.CompareTo(toDate) <= 0)
            .SumAsync(e => e.Amount);

        var waste = await _context.StockMovements
            .Where(m => m.Type == "merma" && m.Date.Substring(0, 10).CompareTo(fromDate) >= 0 && m.Date.Substring(0, 10).CompareTo(toDate) <= 0)
            .SumAsync(m => m.Quantity * m.Cost);

        var allPayments = await _context.Payments
            .Where(p => p.Date.Substring(0, 10).CompareTo(fromDate) >= 0 && p.Date.Substring(0, 10).CompareTo(toDate) <= 0)
            .SumAsync(p => p.Amount);

        var profit = revenue - cost - expenses - waste;

        return Ok(new FinancialSummaryDto(
            revenue,
            cost,
            expenses,
            waste,
            profit,
            allPayments,
            completedOrders.Count
        ));
    }
}

[ApiController]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private readonly FloreriaDbContext _context;
    private readonly IExcelExportService _excelService;

    public ExportController(FloreriaDbContext context, IExcelExportService excelService)
    {
        _context = context;
        _excelService = excelService;
    }

    [HttpGet("orders")]
    public async Task<IActionResult> ExportOrders([FromQuery] string? from, [FromQuery] string? to)
    {
        var q = _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(from))
            q = q.Where(o => o.DeliveryDate.CompareTo(from) >= 0);

        if (!string.IsNullOrWhiteSpace(to))
            q = q.Where(o => o.DeliveryDate.CompareTo(to) <= 0);

        var orders = await q.OrderByDescending(o => o.DeliveryDate).ToListAsync();
        var bytes = _excelService.ExportOrdersToExcel(orders);

        var fileName = $"Floreria_Pedidos_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("attendance")]
    public async Task<IActionResult> ExportAttendance(
        [FromQuery] string? date,
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        var q = _context.Attendances
            .Include(a => a.User)
                .ThenInclude(u => u!.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(date))
        {
            q = q.Where(a => a.Date == date);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(from))
                q = q.Where(a => a.Date.CompareTo(from) >= 0);
            if (!string.IsNullOrWhiteSpace(to))
                q = q.Where(a => a.Date.CompareTo(to) <= 0);
        }

        var attendances = await q.ToListAsync();
        var bytes = _excelService.ExportAttendanceToExcel(attendances);

        var fileName = $"Floreria_Asistencia_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("inventory")]
    public async Task<IActionResult> ExportInventory()
    {
        var materials = await _context.Materials.ToListAsync();

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

        var bytes = _excelService.ExportInventoryToExcel(materials, reservedMap);
        var fileName = $"Floreria_Inventario_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("financial")]
    public async Task<IActionResult> ExportFinancial([FromQuery] string? from, [FromQuery] string? to)
    {
        var fromDate = string.IsNullOrWhiteSpace(from) ? DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd") : from;
        var toDate = string.IsNullOrWhiteSpace(to) ? DateTime.UtcNow.ToString("yyyy-MM-dd") : to;

        var completedOrders = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Where(o => o.Status == "entregado" && o.DeliveryDate.CompareTo(fromDate) >= 0 && o.DeliveryDate.CompareTo(toDate) <= 0)
            .ToListAsync();

        decimal revenue = completedOrders.Sum(o => o.Items.Sum(i => i.Price * i.Quantity) - o.Discount + o.Shipping);
        decimal cost = completedOrders.Sum(o => o.Items.Sum(i => i.Labor * i.Quantity));

        var expenses = await _context.Expenses
            .Where(e => e.Date.CompareTo(fromDate) >= 0 && e.Date.CompareTo(toDate) <= 0)
            .ToListAsync();

        var expTotal = expenses.Sum(e => e.Amount);

        var waste = await _context.StockMovements
            .Where(m => m.Type == "merma" && m.Date.Substring(0, 10).CompareTo(fromDate) >= 0 && m.Date.Substring(0, 10).CompareTo(toDate) <= 0)
            .SumAsync(m => m.Quantity * m.Cost);

        var allPayments = await _context.Payments
            .Where(p => p.Date.Substring(0, 10).CompareTo(fromDate) >= 0 && p.Date.Substring(0, 10).CompareTo(toDate) <= 0)
            .SumAsync(p => p.Amount);

        var profit = revenue - cost - expTotal - waste;

        var summary = new FinancialSummaryDto(revenue, cost, expTotal, waste, profit, allPayments, completedOrders.Count);
        var bytes = _excelService.ExportFinancialReportToExcel(summary, expenses, fromDate, toDate);

        var fileName = $"Floreria_Financiero_{fromDate}_{toDate}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
