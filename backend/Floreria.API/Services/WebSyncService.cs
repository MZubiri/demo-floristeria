using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    private static DateTime? _lastSyncTime;
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
                _logger.LogWarning("No se pudo autenticar con la tienda web: {StatusCode}", response.StatusCode);
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
            _logger.LogError(ex, "Error al autenticarse en la tienda web {BaseUrl}", BaseUrl);
        }

        return null;
    }

    public async Task<SyncResultDto> SyncAllAsync()
    {
        try
        {
            int productsCount = await SyncProductsAsync();
            int ordersCount = await SyncOrdersAsync();
            _lastSyncTime = DateTime.UtcNow;

            return new SyncResultDto(
                true,
                $"Sincronización con Tienda Web completada: {productsCount} productos y {ordersCount} pedidos nuevos/actualizados.",
                productsCount,
                ordersCount,
                _lastSyncTime.Value
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la sincronización completa con la tienda web");
            return new SyncResultDto(
                false,
                $"Error al sincronizar con la tienda web: {ex.Message}",
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

        var response = await _httpClient.GetAsync($"{BaseUrl}/api/products");
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("No se pudieron obtener productos de la web: {StatusCode}", response.StatusCode);
            return 0;
        }

        var json = await response.Content.ReadAsStringAsync();
        var webProducts = JsonSerializer.Deserialize<List<WebProductResponse>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<WebProductResponse>();

        if (!webProducts.Any()) return 0;

        var existingProducts = await context.Products
            .Include(p => p.Recipe)
            .ToListAsync();

        var materials = await context.Materials.ToListAsync();
        var defaultRosa = materials.FirstOrDefault(m => m.Id == "rosa");
        var defaultEuca = materials.FirstOrDefault(m => m.Id == "euca");
        var defaultPapel = materials.FirstOrDefault(m => m.Id == "papel");
        var defaultCinta = materials.FirstOrDefault(m => m.Id == "cinta");
        var defaultTarjeta = materials.FirstOrDefault(m => m.Id == "tarjeta");

        int synced = 0;

        foreach (var wp in webProducts)
        {
            var prodId = $"web_{wp.Id}";
            var existing = existingProducts.FirstOrDefault(p => p.Id == prodId || p.Name.ToLower() == wp.NameEs.ToLower());

            if (existing == null)
            {
                var newProduct = new Product
                {
                    Id = prodId,
                    Name = wp.NameEs,
                    Category = MapCategory(wp.Category),
                    Price = wp.Price,
                    Labor = Math.Round(wp.Price * 0.12m),
                    Image = wp.Image.StartsWith("http") ? wp.Image : $"{BaseUrl}{wp.Image}",
                    Description = wp.DescriptionEs ?? "",
                    IsActive = wp.IsActive
                };

                // Asignar receta base estimada para que descuente inventario en el ERP
                if (defaultRosa != null)
                {
                    newProduct.Recipe.Add(new ProductRecipe { MaterialId = defaultRosa.Id, Quantity = 12, UnitCost = defaultRosa.Cost });
                }
                if (defaultEuca != null)
                {
                    newProduct.Recipe.Add(new ProductRecipe { MaterialId = defaultEuca.Id, Quantity = 2, UnitCost = defaultEuca.Cost });
                }
                if (defaultPapel != null)
                {
                    newProduct.Recipe.Add(new ProductRecipe { MaterialId = defaultPapel.Id, Quantity = 2, UnitCost = defaultPapel.Cost });
                }
                if (defaultCinta != null)
                {
                    newProduct.Recipe.Add(new ProductRecipe { MaterialId = defaultCinta.Id, Quantity = 1, UnitCost = defaultCinta.Cost });
                }
                if (defaultTarjeta != null)
                {
                    newProduct.Recipe.Add(new ProductRecipe { MaterialId = defaultTarjeta.Id, Quantity = 1, UnitCost = defaultTarjeta.Cost });
                }

                context.Products.Add(newProduct);
                synced++;
            }
            else
            {
                // Actualizar datos del producto web
                existing.Name = wp.NameEs;
                existing.Category = MapCategory(wp.Category);
                existing.Price = wp.Price;
                if (!string.IsNullOrWhiteSpace(wp.Image))
                {
                    existing.Image = wp.Image.StartsWith("http") ? wp.Image : $"{BaseUrl}{wp.Image}";
                }
                existing.Description = wp.DescriptionEs ?? existing.Description;
                existing.IsActive = wp.IsActive;
                synced++;
            }
        }

        await context.SaveChangesAsync();
        _lastSyncTime = DateTime.UtcNow;
        return synced;
    }

    public async Task<int> SyncOrdersAsync()
    {
        var token = await GetWebAuthTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("No se pudo obtener token de autenticación para sincronizar pedidos de la web.");
            return 0;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Error al consultar pedidos de la web: {StatusCode}", response.StatusCode);
            return 0;
        }

        var json = await response.Content.ReadAsStringAsync();
        var webOrders = JsonSerializer.Deserialize<List<WebOrderResponse>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<WebOrderResponse>();

        if (!webOrders.Any()) return 0;

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FloreriaDbContext>();

        var existingOrders = await context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .ToListAsync();

        var dbProducts = await context.Products
            .Include(p => p.Recipe)
            .ToListAsync();

        int synced = 0;

        foreach (var wo in webOrders)
        {
            var existing = existingOrders.FirstOrDefault(o => o.Number == wo.OrderCode);

            if (existing == null)
            {
                var orderId = Guid.NewGuid().ToString();
                var localStatus = MapWebStatusToLocal(wo.Status);

                var order = new Order
                {
                    Id = orderId,
                    Number = wo.OrderCode,
                    CreatedAt = wo.CreatedAt.ToString("o"),
                    Customer = wo.CustomerName,
                    Phone = wo.CustomerPhone,
                    Email = wo.CustomerEmail ?? "",
                    Recipient = wo.CustomerName,
                    RecipientPhone = wo.CustomerPhone,
                    Address = wo.DeliveryAddress,
                    Area = string.IsNullOrWhiteSpace(wo.DeliveryMunicipality) ? "Caldas" : wo.DeliveryMunicipality,
                    DeliveryDate = string.IsNullOrWhiteSpace(wo.DeliveryDate) ? DateTime.UtcNow.ToString("yyyy-MM-dd") : wo.DeliveryDate,
                    Time = string.IsNullOrWhiteSpace(wo.DeliveryTime) ? "Por convenir" : wo.DeliveryTime,
                    DeliveryMethod = "Tienda Web",
                    Priority = "Normal",
                    Discount = 0,
                    Shipping = wo.DeliveryFee,
                    HasCard = !string.IsNullOrWhiteSpace(wo.CardMessage),
                    CardMessage = wo.CardMessage ?? "",
                    Notes = $"[Tienda Web La Carreta] Tarjeta: {wo.CardStyle ?? "Clásica"} | Remitente: {wo.CardSender ?? ""} | Notas: {wo.SpecialNotes ?? ""}",
                    Status = localStatus,
                    Consumed = localStatus == "entregado",
                    IsDirectSale = false
                };

                if (wo.Items != null)
                {
                    foreach (var item in wo.Items)
                    {
                        var prod = dbProducts.FirstOrDefault(p =>
                            p.Id == $"web_{item.ProductId}" ||
                            p.Name.ToLower() == item.ProductName.ToLower());

                        var recipeJson = prod != null && prod.Recipe.Any()
                            ? JsonSerializer.Serialize(prod.Recipe.Select(r => new IngredientDto(r.MaterialId, r.Quantity, r.UnitCost)))
                            : "[]";

                        order.Items.Add(new OrderItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            OrderId = orderId,
                            ProductId = prod?.Id ?? $"web_{item.ProductId}",
                            Name = item.ProductName,
                            Quantity = item.Quantity,
                            Price = item.UnitPrice,
                            Labor = prod?.Labor ?? Math.Round(item.UnitPrice * 0.12m),
                            Notes = "Item de pedido web",
                            RecipeJson = recipeJson
                        });
                    }
                }

                // Registrar pago si no está cancelado
                if (localStatus != "cancelado")
                {
                    order.Payments.Add(new Payment
                    {
                        Id = Guid.NewGuid().ToString(),
                        OrderId = orderId,
                        Amount = wo.TotalAmount,
                        Method = "Transferencia Web / Pasarela",
                        Reference = $"Web {wo.OrderCode}",
                        Date = wo.CreatedAt.ToString("o")
                    });
                }

                order.History.Add(new OrderHistory
                {
                    OrderId = orderId,
                    Date = DateTime.UtcNow.ToString("o"),
                    Title = "Pedido sincronizado desde Tienda Web",
                    Note = $"Importado desde floreria.molinazdev.lat (Código {wo.OrderCode})"
                });

                context.Orders.Add(order);
                synced++;
            }
            else
            {
                // Si el pedido ya existe en el ERP, sincronizar estado si en la web cambió
                var mappedStatus = MapWebStatusToLocal(wo.Status);
                if (existing.Status != mappedStatus && existing.Status != "entregado")
                {
                    existing.Status = mappedStatus;
                    existing.History.Add(new OrderHistory
                    {
                        OrderId = existing.Id,
                        Date = DateTime.UtcNow.ToString("o"),
                        Title = "Estado actualizado desde Tienda Web",
                        Note = $"Nuevo estado sincronizado: {mappedStatus}"
                    });
                    synced++;
                }
            }
        }

        await context.SaveChangesAsync();
        _lastSyncTime = DateTime.UtcNow;
        return synced;
    }

    public async Task<bool> PushOrderStatusAsync(string orderCode, string newStatus)
    {
        try
        {
            var token = await GetWebAuthTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;

            var webStatus = MapLocalStatusToWeb(newStatus);
            var payload = new { status = webStatus };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Put, $"{BaseUrl}/api/orders/status-by-code/{orderCode}")
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await _httpClient.SendAsync(request);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo actualizar el estado del pedido {OrderCode} en la tienda web", orderCode);
            return false;
        }
    }

    public async Task<SyncStatusDto> GetStatusAsync()
    {
        bool isConnected = false;
        string message = "Desconectado";
        int totalOrders = 0;
        int totalProducts = 0;

        try
        {
            var res = await _httpClient.GetAsync($"{BaseUrl}/api/products");
            isConnected = res.IsSuccessStatusCode;
            if (isConnected)
            {
                message = "Conectado a Florería La Carreta (floreria.molinazdev.lat)";
            }
        }
        catch (Exception ex)
        {
            message = $"Error de conexión con la web: {ex.Message}";
        }

        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FloreriaDbContext>();
            totalOrders = await context.Orders.CountAsync();
            totalProducts = await context.Products.CountAsync();
        }

        return new SyncStatusDto(
            isConnected,
            BaseUrl,
            _lastSyncTime,
            totalOrders,
            totalProducts,
            message
        );
    }

    private static string MapCategory(string cat)
    {
        return cat.ToLower() switch
        {
            "ramos" => "Ramos",
            "arreglos" => "Premium",
            "plantas" => "Detalles",
            "condolencias" => "Condolencias",
            "premium" => "Premium",
            _ => "Ramos"
        };
    }

    private static string MapWebStatusToLocal(string webStatus)
    {
        return webStatus.ToLower() switch
        {
            "pendiente" => "recibido",
            "enpreparacion" => "preparando",
            "encamino" => "camino",
            "entregado" => "entregado",
            "cancelado" => "cancelado",
            _ => "recibido"
        };
    }

    private static string MapLocalStatusToWeb(string localStatus)
    {
        return localStatus.ToLower() switch
        {
            "recibido" => "Pendiente",
            "confirmado" => "Pendiente",
            "preparando" => "EnPreparacion",
            "listo" => "EnPreparacion",
            "camino" => "EnCamino",
            "entregado" => "Entregado",
            "cancelado" => "Cancelado",
            _ => "Pendiente"
        };
    }

    private class WebProductResponse
    {
        public int Id { get; set; }
        public string NameEs { get; set; } = string.Empty;
        public string? DescriptionEs { get; set; }
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    private class WebOrderResponse
    {
        public int Id { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        public string DeliveryAddress { get; set; } = string.Empty;
        public string? DeliveryMunicipality { get; set; }
        public decimal DeliveryFee { get; set; }
        public string DeliveryDate { get; set; } = string.Empty;
        public string DeliveryTime { get; set; } = string.Empty;
        public string? CardStyle { get; set; }
        public string? CardSender { get; set; }
        public string? CardMessage { get; set; }
        public string? SpecialNotes { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<WebOrderItemResponse>? Items { get; set; }
    }

    private class WebOrderItemResponse
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}
