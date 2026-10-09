using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Floreria.API.Data;
using Floreria.API.DTOs;
using Floreria.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Floreria.API.Services;

public class WebSyncService : IWebSyncService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebSyncService> _logger;

    private static DateTime? _lastSyncTime = DateTime.UtcNow;
    private static string? _cachedWebToken;
    private static DateTime _tokenExpiresAt = DateTime.MinValue;

    public WebSyncService(
        HttpClient httpClient,
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<WebSyncService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    private string BaseUrl => _configuration["WebShop:BaseUrl"]?.TrimEnd('/') ?? "https://floreria.molinazdev.lat";
    private string Username => _configuration["WebShop:Username"] ?? "admin";
    private string Password => _configuration["WebShop:Password"] ?? "AdminDemo2026";

    public async Task<SyncResultDto> SyncAllAsync()
    {
        try
        {
            int updatedStocks = await CheckAndSyncStockAvailabilityAsync();
            int productsCount = await SyncProductsAsync();
            int ordersCount = await SyncOrdersAsync();
            _lastSyncTime = DateTime.UtcNow;

            return new SyncResultDto(
                true,
                $"Base de datos única compartida en tiempo real: {productsCount} productos y {ordersCount} pedidos activos. {updatedStocks} arreglos actualizados por stock.",
                productsCount,
                ordersCount,
                _lastSyncTime.Value
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la comprobación de sincronización unificada");
            return new SyncResultDto(
                false,
                $"Error en base de datos unificada: {ex.Message}",
                0,
                0,
                DateTime.UtcNow
            );
        }
    }

    public async Task<int> SyncProductsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FloreriaDbContext>();
        return await context.Products.CountAsync(p => p.IsActive);
    }

    public async Task<int> SyncOrdersAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FloreriaDbContext>();
        return await context.Orders.CountAsync();
    }

    public async Task<PushProductResultDto> PushProductToWebAsync(Product product)
    {
        // En base de datos unificada, el producto ya reside en la misma base de datos compartida
        return await Task.FromResult(new PushProductResultDto(
            true,
            product.Id.ToString(),
            "Producto sincronizado en la base de datos unificada."
        ));
    }

    public async Task<bool> DeleteProductFromWebAsync(string productId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FloreriaDbContext>();
        if (int.TryParse(productId, out int id))
        {
            var p = await context.Products.FindAsync(id);
            if (p != null)
            {
                p.IsActive = false;
                await context.SaveChangesAsync();
                return true;
            }
        }
        return false;
    }

    public async Task<bool> PushOrderStatusAsync(string orderCode, string newStatus)
    {
        // En base de datos unificada, el pedido ya se actualiza en la tabla compartida Orders
        return await Task.FromResult(true);
    }

    public async Task<bool> PushOrderFinalPhotoAsync(string orderCode, string photoUrl)
    {
        // En base de datos unificada, la foto ya se actualiza en la tabla compartida Orders
        return await Task.FromResult(true);
    }

    public async Task<string?> PushOrderToWebAsync(Order order)
    {
        // En base de datos unificada, la venta ya se almacena en la tabla compartida Orders
        return await Task.FromResult(order.Number);
    }

    /// <summary>
    /// Sube una fotografía al endpoint de la tienda web para acceso público directo si está configurado.
    /// </summary>
    public async Task<string?> UploadImageToWebAsync(Stream fileStream, string fileName, string contentType)
    {
        try
        {
            var token = await GetWebAuthTokenAsync();
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/upload");
            if (!string.IsNullOrEmpty(token))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(streamContent, "file", fileName);
            req.Content = content;

            var res = await _httpClient.SendAsync(req);
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("url", out var urlProp))
                {
                    var path = urlProp.GetString();
                    return path?.StartsWith("http", StringComparison.OrdinalIgnoreCase) == true
                        ? path
                        : $"{BaseUrl}{path}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo subir la imagen a la tienda web remota, se mantendrá copia local");
        }

        return null;
    }

    /// <summary>
    /// Comprueba el stock de materiales requeridos para cada arreglo activo.
    /// Si los insumos son insuficientes, se desactiva automáticamente para evitar ventas sin disponibilidad.
    /// Si los insumos se reabastecen, se reactiva automáticamente.
    /// </summary>
    public async Task<int> CheckAndSyncStockAvailabilityAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FloreriaDbContext>();

            var products = await context.Products
                .Include(p => p.Recipe)
                .ToListAsync();

            var materials = await context.Materials
                .ToDictionaryAsync(m => m.Id, m => m.Stock);

            int changesCount = 0;

            foreach (var prod in products)
            {
                if (prod.Recipe == null || !prod.Recipe.Any())
                {
                    continue;
                }

                bool canFulfill = true;
                foreach (var item in prod.Recipe)
                {
                    if (materials.TryGetValue(item.MaterialId, out var stock))
                    {
                        if (stock < item.Quantity)
                        {
                            canFulfill = false;
                            break;
                        }
                    }
                    else
                    {
                        canFulfill = false;
                        break;
                    }
                }

                if (prod.IsActive != canFulfill)
                {
                    prod.IsActive = canFulfill;
                    changesCount++;
                    _logger.LogInformation(
                        "Disponibilidad de '{Name}' actualizada a: {Status} por validación de insumos",
                        prod.Name,
                        canFulfill ? "ACTIVO" : "AGOTADO / DESACTIVADO"
                    );
                }
            }

            if (changesCount > 0)
            {
                await context.SaveChangesAsync();
            }

            return changesCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar y actualizar disponibilidad de arreglos por insumos");
            return 0;
        }
    }

    public async Task<SyncStatusDto> GetStatusAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FloreriaDbContext>();
        int totalOrders = await context.Orders.CountAsync();
        int totalProducts = await context.Products.CountAsync();

        return new SyncStatusDto(
            true,
            "Base de Datos MySQL Unificada (floreria_lacarreta_db)",
            _lastSyncTime,
            totalOrders,
            totalProducts,
            "Conectado a la base de datos compartida en tiempo real. 100% Sincronizado sin retraso."
        );
    }

    private async Task<string?> GetWebAuthTokenAsync()
    {
        if (!string.IsNullOrEmpty(_cachedWebToken) && DateTime.UtcNow < _tokenExpiresAt)
        {
            return _cachedWebToken;
        }

        try
        {
            var loginPayload = new { username = Username, password = Password };
            var content = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{BaseUrl}/api/auth/login", content);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("token", out var tokenProp))
            {
                _cachedWebToken = tokenProp.GetString();
                _tokenExpiresAt = DateTime.UtcNow.AddHours(23);
                return _cachedWebToken;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("No se pudo obtener token de la tienda web: {Msg}", ex.Message);
        }

        return null;
    }
}
