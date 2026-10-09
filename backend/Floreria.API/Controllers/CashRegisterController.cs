using Floreria.API.Data;
using Floreria.API.DTOs;
using Floreria.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Floreria.API.Controllers;

[ApiController]
[Route("api/cash-register")]
public class CashRegisterController : ControllerBase
{
    private readonly FloreriaDbContext _context;
    private readonly ILogger<CashRegisterController> _logger;

    public CashRegisterController(FloreriaDbContext context, ILogger<CashRegisterController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<CashRegisterSummaryDto>> GetSummary([FromQuery] string? date)
    {
        var targetDate = string.IsNullOrWhiteSpace(date)
            ? DateTime.UtcNow.ToString("yyyy-MM-dd")
            : date.Trim();

        // Obtener el saldo inicial del último cierre registrado
        var lastClosure = await _context.CashRegisterClosures
            .OrderByDescending(c => c.ClosedAt)
            .FirstOrDefaultAsync();

        decimal openingBalance = lastClosure?.ActualCash ?? 0;

        // Obtener pedidos del día que no estén cancelados
        var orders = await _context.Orders
            .Include(o => o.Payments)
            .Where(o => o.DeliveryDate == targetDate && o.Status != "cancelado")
            .ToListAsync();

        decimal cashSales = 0;
        decimal electronicSales = 0;

        foreach (var order in orders)
        {
            foreach (var p in order.Payments)
            {
                var method = (p.Method ?? "").ToLower();
                if (method.Contains("efectivo") || method.Contains("cash"))
                {
                    cashSales += p.Amount;
                }
                else
                {
                    electronicSales += p.Amount;
                }
            }
        }

        // Gastos pagados en efectivo en esa fecha
        var expenses = await _context.Expenses
            .Where(e => e.Date == targetDate && (e.Method.ToLower().Contains("efectivo") || e.Method.ToLower().Contains("cash")))
            .ToListAsync();

        decimal cashExpenses = expenses.Sum(e => e.Amount);
        decimal totalSales = cashSales + electronicSales;
        decimal expectedCash = Math.Max(0, openingBalance + cashSales - cashExpenses);

        return Ok(new CashRegisterSummaryDto(
            targetDate,
            openingBalance,
            cashSales,
            electronicSales,
            totalSales,
            cashExpenses,
            expectedCash,
            orders.Count
        ));
    }

    [HttpPost("close")]
    public async Task<ActionResult<CashRegisterClosureDto>> CloseRegister([FromBody] CloseCashRegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Date))
        {
            return BadRequest(new { message = "La fecha del cierre es requerida." });
        }

        var targetDate = req.Date.Trim();

        // Re-calcular ventas del día para asegurar consistencia contable
        var orders = await _context.Orders
            .Include(o => o.Payments)
            .Where(o => o.DeliveryDate == targetDate && o.Status != "cancelado")
            .ToListAsync();

        decimal cashSales = 0;
        decimal electronicSales = 0;

        foreach (var order in orders)
        {
            foreach (var p in order.Payments)
            {
                var method = (p.Method ?? "").ToLower();
                if (method.Contains("efectivo") || method.Contains("cash"))
                {
                    cashSales += p.Amount;
                }
                else
                {
                    electronicSales += p.Amount;
                }
            }
        }

        var expenses = await _context.Expenses
            .Where(e => e.Date == targetDate && (e.Method.ToLower().Contains("efectivo") || e.Method.ToLower().Contains("cash")))
            .ToListAsync();

        decimal totalExpenses = expenses.Sum(e => e.Amount);
        decimal totalSales = cashSales + electronicSales;
        decimal expectedCash = req.OpeningBalance + cashSales - totalExpenses;
        decimal difference = req.ActualCash - expectedCash;

        var closure = new CashRegisterClosure
        {
            Id = Guid.NewGuid().ToString(),
            Date = targetDate,
            ClosedAt = DateTime.UtcNow.ToString("o"),
            CashierName = string.IsNullOrWhiteSpace(req.CashierName) ? "Cajero Principal" : req.CashierName.Trim(),
            OpeningBalance = req.OpeningBalance,
            CashSales = cashSales,
            ElectronicSales = electronicSales,
            TotalSales = totalSales,
            TotalExpenses = totalExpenses,
            ExpectedCash = expectedCash,
            ActualCash = req.ActualCash,
            Difference = difference,
            OrderCount = orders.Count,
            Notes = req.Notes?.Trim()
        };

        _context.CashRegisterClosures.Add(closure);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cierre de caja registrado para {Date}: Esperado={Expected}, Real={Actual}, Diferencia={Diff}",
            closure.Date, closure.ExpectedCash, closure.ActualCash, closure.Difference);

        return Ok(MapToDto(closure));
    }

    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<CashRegisterClosureDto>>> GetHistory()
    {
        var closures = await _context.CashRegisterClosures
            .OrderByDescending(c => c.ClosedAt)
            .Take(60)
            .ToListAsync();

        return Ok(closures.Select(MapToDto));
    }

    private static CashRegisterClosureDto MapToDto(CashRegisterClosure c) => new(
        c.Id,
        c.Date,
        c.ClosedAt,
        c.CashierName,
        c.OpeningBalance,
        c.CashSales,
        c.ElectronicSales,
        c.TotalSales,
        c.TotalExpenses,
        c.ExpectedCash,
        c.ActualCash,
        c.Difference,
        c.OrderCount,
        c.Notes
    );
}
