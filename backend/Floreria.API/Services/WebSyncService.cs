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
            await CheckAndSyncStockAvailabilityAsync();
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
                    IsActive = wp.IsActive,
                    Sku = !string.IsNullOrWhiteSpace(wp.Sku) ? wp.Sku : $"LC-{MapCategory(wp.Category)[..Math.Min(3, MapCategory(wp.Category).Length)].ToUpper()}-{wp.Id:D3}"
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
                if (string.IsNullOrWhiteSpace(existing.Sku))
                {
                    existing.Sku = !string.IsNullOrWhiteSpace(wp.Sku) ? wp.Sku : $"LC-{existing.Category[..Math.Min(3, existing.Category.Length)].ToUpper()}-{wp.Id:D3}";
                }
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
                    Recipient = string.IsNullOrWhiteSpace(wo.RecipientName) ? wo.CustomerName : wo.RecipientName,
                    RecipientPhone = string.IsNullOrWhiteSpace(wo.RecipientPhone) ? wo.CustomerPhone : wo.RecipientPhone,
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

    public async Task<PushProductResultDto> PushProductToWebAsync(Product product)
    {
        try
        {
            var token = await GetWebAuthTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return new PushProductResultDto(false, null, "No se pudo autenticar con la tienda web.");
            }

            var webCategory = MapCategoryToWeb(product.Category);
            bool isUpdate = product.Id.StartsWith("web_") && int.TryParse(product.Id[4..], out _);

            if (isUpdate)
            {
                int idToUpdate = int.Parse(product.Id[4..]);
                var updatePayload = new
                {
                    id = idToUpdate,
                    nameEs = product.Name,
                    nameEn = product.Name,
                    descriptionEs = string.IsNullOrWhiteSpace(product.Description) ? product.Name : product.Description,
                    descriptionEn = string.IsNullOrWhiteSpace(product.Description) ? product.Name : product.Description,
                    price = product.Price,
                    category = webCategory,
                    image = string.IsNullOrWhiteSpace(product.Image) ? "assets/rosas.svg" : product.Image,
                    featured = false,
                    isActive = product.IsActive,
                    sku = !string.IsNullOrWhiteSpace(product.Sku) ? product.Sku : $"LC-{product.Category.ToUpper()[..Math.Min(3, product.Category.Length)]}-{product.Id.Replace("web_", "").PadLeft(3, '0')}",
                    occasionEs = "[\"Amor\",\"Aniversario\"]",
                    occasionEn = "[\"Love\",\"Anniversary\"]"
                };

                using var putReq = new HttpRequestMessage(HttpMethod.Put, $"{BaseUrl}/api/products/{idToUpdate}")
                {
                    Content = new StringContent(JsonSerializer.Serialize(updatePayload), Encoding.UTF8, "application/json")
                };
                putReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var putRes = await _httpClient.SendAsync(putReq);
                if (putRes.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Producto {Id} ({Name}) actualizado en la tienda web.", product.Id, product.Name);
                    return new PushProductResultDto(true, product.Id, "Producto actualizado con éxito en la tienda web.");
                }

                return new PushProductResultDto(false, product.Id, $"Error en tienda web: {putRes.StatusCode}");
            }
            else
            {
                // Producto nuevo: crear en la tienda web
                var createPayload = new
                {
                    nameEs = product.Name,
                    nameEn = product.Name,
                    descriptionEs = string.IsNullOrWhiteSpace(product.Description) ? product.Name : product.Description,
                    descriptionEn = string.IsNullOrWhiteSpace(product.Description) ? product.Name : product.Description,
                    price = product.Price,
                    category = webCategory,
                    image = string.IsNullOrWhiteSpace(product.Image) ? "assets/rosas.svg" : product.Image,
                    featured = false,
                    isActive = product.IsActive,
                    sku = !string.IsNullOrWhiteSpace(product.Sku) ? product.Sku : $"LC-{product.Category.ToUpper()[..Math.Min(3, product.Category.Length)]}-001",
                    occasionEs = "[\"Amor\",\"Aniversario\"]",
                    occasionEn = "[\"Love\",\"Anniversary\"]"
                };

                using var postReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/products")
                {
                    Content = new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json")
                };
                postReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var postRes = await _httpClient.SendAsync(postReq);
                if (postRes.IsSuccessStatusCode)
                {
                    var postJson = await postRes.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(postJson);
                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                    {
                        var newWebId = $"web_{idProp.GetInt32()}";
                        _logger.LogInformation("Producto {Name} creado en tienda web con ID {NewWebId}.", product.Name, newWebId);
                        return new PushProductResultDto(true, newWebId, "Producto creado y publicado con éxito en la tienda web.");
                    }
                }

                return new PushProductResultDto(false, null, $"Error al crear en tienda web: {postRes.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al sincronizar producto {Id} con la tienda web", product.Id);
            return new PushProductResultDto(false, null, $"Error: {ex.Message}");
        }
    }

    public async Task<bool> DeleteProductFromWebAsync(string productId)
    {
        try
        {
            if (!productId.StartsWith("web_") || !int.TryParse(productId[4..], out int webId))
            {
                return false;
            }

            var token = await GetWebAuthTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;

            using var req = new HttpRequestMessage(HttpMethod.Delete, $"{BaseUrl}/api/products/{webId}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await _httpClient.SendAsync(req);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar producto {ProductId} de la tienda web", productId);
            return false;
        }
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

            // Buscar el pedido en la tienda web por su OrderCode
            using var searchReq = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/orders?search={orderCode}");
            searchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var searchRes = await _httpClient.SendAsync(searchReq);

            if (searchRes.IsSuccessStatusCode)
            {
                var searchJson = await searchRes.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(searchJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var webId = doc.RootElement[0].GetProperty("id").GetInt32();
                    using var updateReq = new HttpRequestMessage(HttpMethod.Put, $"{BaseUrl}/api/orders/{webId}/status")
                    {
                        Content = content
                    };
                    updateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var putRes = await _httpClient.SendAsync(updateReq);
                    return putRes.IsSuccessStatusCode;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo actualizar el estado del pedido {OrderCode} en la tienda web", orderCode);
            return false;
        }
    }

    public async Task<bool> PushOrderFinalPhotoAsync(string orderCode, string photoUrl)
    {
        try
        {
            var token = await GetWebAuthTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;

            var payload = new { finalArrangementPhotoUrl = photoUrl };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var searchReq = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/orders?search={orderCode}");
            searchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var searchRes = await _httpClient.SendAsync(searchReq);

            if (searchRes.IsSuccessStatusCode)
            {
                var searchJson = await searchRes.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(searchJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var webId = doc.RootElement[0].GetProperty("id").GetInt32();
                    using var updateReq = new HttpRequestMessage(HttpMethod.Put, $"{BaseUrl}/api/orders/{webId}/status")
                    {
                        Content = content
                    };
                    updateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var putRes = await _httpClient.SendAsync(updateReq);
                    if (putRes.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Foto final del arreglo para pedido {OrderCode} publicada con éxito en la tienda web.", orderCode);
                        return true;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo sincronizar la foto final del pedido {OrderCode} con la tienda web", orderCode);
            return false;
        }
    }

    public async Task<string?> PushOrderToWebAsync(Order order)
    {
        try
        {
            var token = await GetWebAuthTokenAsync();
            var items = new List<object>();

            foreach (var item in order.Items)
            {
                int webProdId = 1;
                if (item.ProductId.StartsWith("web_") && int.TryParse(item.ProductId[4..], out int pid))
                {
                    webProdId = pid;
                }

                items.Add(new
                {
                    productId = webProdId,
                    productName = item.Name,
                    quantity = item.Quantity,
                    unitPrice = item.Price
                });
            }

            var payload = new
            {
                customerName = order.Customer,
                customerPhone = order.Phone,
                customerEmail = string.IsNullOrWhiteSpace(order.Email) ? "local@florerialacarreta.com" : order.Email,
                recipientName = string.IsNullOrWhiteSpace(order.Recipient) ? order.Customer : order.Recipient,
                recipientPhone = string.IsNullOrWhiteSpace(order.RecipientPhone) ? order.Phone : order.RecipientPhone,
                deliveryAddress = string.IsNullOrWhiteSpace(order.Address) ? "Venta en tienda / Local" : order.Address,
                deliveryMunicipality = string.IsNullOrWhiteSpace(order.Area) ? "Caldas" : order.Area,
                deliveryFee = order.Shipping,
                deliveryDate = string.IsNullOrWhiteSpace(order.DeliveryDate) ? DateTime.UtcNow.ToString("yyyy-MM-dd") : order.DeliveryDate,
                deliveryTime = string.IsNullOrWhiteSpace(order.Time) ? "15:00" : order.Time,
                cardStyle = "Clásica Floral",
                cardSender = order.Customer,
                cardMessage = order.CardMessage ?? "",
                specialNotes = order.Notes ?? "",
                items
            };

            using var postReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/orders")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            var postRes = await _httpClient.SendAsync(postReq);
            if (postRes.IsSuccessStatusCode)
            {
                var resJson = await postRes.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(resJson);
                if (doc.RootElement.TryGetProperty("orderCode", out var codeProp))
                {
                    var webOrderCode = codeProp.GetString();
                    var webOrderId = doc.RootElement.GetProperty("id").GetInt32();

                    if (order.Status == "entregado" && !string.IsNullOrEmpty(token))
                    {
                        var statusPayload = new { status = "Entregado" };
                        using var putReq = new HttpRequestMessage(HttpMethod.Put, $"{BaseUrl}/api/orders/{webOrderId}/status")
                        {
                            Content = new StringContent(JsonSerializer.Serialize(statusPayload), Encoding.UTF8, "application/json")
                        };
                        putReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        await _httpClient.SendAsync(putReq);
                    }

                    return webOrderCode;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo sincronizar pedido {OrderId} hacia la tienda web", order.Id);
        }
        return null;
    }

    public async Task<string?> UploadImageToWebAsync(Stream fileStream, string fileName, string contentType)
    {
        try
        {
            var token = await GetWebAuthTokenAsync();
            if (string.IsNullOrEmpty(token)) return null;

            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(streamContent, "file", fileName);

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/upload")
            {
                Content = content
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await _httpClient.SendAsync(req);
            if (res.IsSuccessStatusCode)
            {
                var jsonStr = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonStr);
                if (doc.RootElement.TryGetProperty("url", out var urlProp))
                {
                    var relUrl = urlProp.GetString();
                    if (!string.IsNullOrEmpty(relUrl))
                    {
                        var fullUrl = relUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                            ? relUrl
                            : $"{BaseUrl}{relUrl}";
                        _logger.LogInformation("Imagen subida a la tienda web exitosamente: {Url}", fullUrl);
                        return fullUrl;
                    }
                }
            }
            else
            {
                _logger.LogWarning("Respuesta fallida de la tienda web al subir imagen: {Status}", res.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al subir imagen a la tienda web: {Msg}", ex.Message);
        }
        return null;
    }

    public async Task<int> CheckAndSyncStockAvailabilityAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FloreriaDbContext>();

            var products = await context.Products
                .Include(p => p.Recipe)
                .ToListAsync();

            var materials = await context.Materials.ToListAsync();
            var matMap = materials.ToDictionary(m => m.Id, m => m.Stock);

            int changesCount = 0;

            foreach (var p in products)
            {
                if (p.Recipe == null || !p.Recipe.Any()) continue;

                // Verificar si todos los insumos requeridos tienen stock suficiente para armar al menos 1 unidad
                bool hasSufficientStock = true;
                string missingMaterialName = "";

                foreach (var r in p.Recipe)
                {
                    decimal currentStock = matMap.TryGetValue(r.MaterialId, out var s) ? s : 0;
                    if (currentStock < r.Quantity || currentStock <= 0)
                    {
                        hasSufficientStock = false;
                        var mat = materials.FirstOrDefault(m => m.Id == r.MaterialId);
                        missingMaterialName = mat?.Name ?? r.MaterialId;
                        break;
                    }
                }

                if (!hasSufficientStock && p.IsActive)
                {
                    // Desactivación automática por falta de insumos
                    p.IsActive = false;
                    changesCount++;
                    _logger.LogInformation("Arreglo {Name} ({Id}) desactivado automáticamente por agotamiento de insumo: {Insumo}", p.Name, p.Id, missingMaterialName);
                    await PushProductToWebAsync(p);
                }
                else if (hasSufficientStock && !p.IsActive)
                {
                    // Reactivación automática tras reposición de inventario
                    p.IsActive = true;
                    changesCount++;
                    _logger.LogInformation("Arreglo {Name} ({Id}) reactivado automáticamente al contar con insumos suficientes.", p.Name, p.Id);
                    await PushProductToWebAsync(p);
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
            _logger.LogError(ex, "Error al verificar y sincronizar disponibilidad de arreglos por insumos");
            return 0;
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

    private static string MapCategoryToWeb(string cat)
    {
        return (cat ?? "").ToLower() switch
        {
            "ramos" => "ramos",
            "premium" => "premium",
            "detalles" => "detalles",
            "condolencias" => "condolencias",
            "plantas" => "plantas",
            _ => "ramos"
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
        public string? Sku { get; set; }
    }

    private class WebOrderResponse
    {
        public int Id { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        public string? RecipientName { get; set; }
        public string? RecipientPhone { get; set; }
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
