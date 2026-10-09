using Floreria.API.DTOs;

namespace Floreria.API.Services;

public record SyncResultDto(
    bool Success,
    string Message,
    int ProductsSynced,
    int OrdersSynced,
    DateTime Timestamp
);

public record SyncStatusDto(
    bool IsConnected,
    string WebShopUrl,
    DateTime? LastSync,
    int TotalOrders,
    int TotalProducts,
    string StatusMessage
);

public record PushProductResultDto(
    bool Success,
    string? WebId,
    string Message
);

public interface IWebSyncService
{
    Task<SyncResultDto> SyncAllAsync();
    Task<int> SyncProductsAsync();
    Task<int> SyncOrdersAsync();
    Task<PushProductResultDto> PushProductToWebAsync(Floreria.API.Models.Product product);
    Task<bool> DeleteProductFromWebAsync(string productId);
    Task<bool> PushOrderStatusAsync(string orderCode, string newStatus);
    Task<string?> PushOrderToWebAsync(Floreria.API.Models.Order order);
    Task<string?> UploadImageToWebAsync(Stream fileStream, string fileName, string contentType);
    Task<SyncStatusDto> GetStatusAsync();
}

