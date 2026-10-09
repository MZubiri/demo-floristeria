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

public interface IWebSyncService
{
    Task<SyncResultDto> SyncAllAsync();
    Task<int> SyncProductsAsync();
    Task<int> SyncOrdersAsync();
    Task<bool> PushOrderStatusAsync(string orderCode, string newStatus);
    Task<SyncStatusDto> GetStatusAsync();
}
