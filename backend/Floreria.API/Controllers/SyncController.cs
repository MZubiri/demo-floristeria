using Floreria.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Floreria.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly IWebSyncService _syncService;

    public SyncController(IWebSyncService syncService)
    {
        _syncService = syncService;
    }

    [HttpPost("all")]
    public async Task<ActionResult<SyncResultDto>> SyncAll()
    {
        var result = await _syncService.SyncAllAsync();
        return Ok(result);
    }

    [HttpPost("orders")]
    public async Task<ActionResult<object>> SyncOrders()
    {
        var count = await _syncService.SyncOrdersAsync();
        return Ok(new { success = true, ordersSynced = count, message = $"Sincronizados {count} pedidos desde la tienda web." });
    }

    [HttpPost("products")]
    public async Task<ActionResult<object>> SyncProducts()
    {
        var count = await _syncService.SyncProductsAsync();
        return Ok(new { success = true, productsSynced = count, message = $"Sincronizados {count} productos desde la tienda web." });
    }

    [HttpGet("status")]
    public async Task<ActionResult<SyncStatusDto>> GetStatus()
    {
        var status = await _syncService.GetStatusAsync();
        return Ok(status);
    }

    /// <summary>
    /// Endpoint de Webhook para recibir notificaciones en tiempo real desde la Tienda Web
    /// Se invoca inmediatamente cuando se crea un pedido o cambia un estado en la tienda web.
    /// </summary>
    [HttpPost("webhook")]
    public async Task<ActionResult<object>> HandleWebShopWebhook([FromBody] object? payload)
    {
        await _syncService.CheckAndSyncStockAvailabilityAsync();
        return Ok(new { success = true, message = "Webhook recibido y procesado en tiempo real." });
    }
}
