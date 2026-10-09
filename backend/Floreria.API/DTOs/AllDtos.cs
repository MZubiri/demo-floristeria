using Floreria.API.Models;

namespace Floreria.API.DTOs;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token, UserDto User);

public record UserDto(
    int Id,
    string Name,
    string Email,
    string Phone,
    int RoleId,
    string RoleName,
    bool IsActive,
    DateTime CreatedAt,
    List<string>? Permissions = null
);

public record CreateUserDto(
    string Name,
    string Email,
    string Password,
    string Phone,
    int RoleId,
    bool IsActive = true
);

public record UpdateUserDto(
    string Name,
    string Email,
    string Phone,
    int RoleId,
    bool IsActive
);

public record ChangePasswordDto(
    string NewPassword
);

public record RoleDto(
    int Id,
    string Name,
    string Description,
    string PermissionsJson
);

public record CreateRoleDto(
    string Name,
    string Description,
    string PermissionsJson = "[]"
);

public record AttendanceDto(
    int Id,
    int UserId,
    string UserName,
    string UserRole,
    string Date,
    string? ClockIn,
    string? ClockOut,
    string Status,
    string? Notes,
    string? DeviceFingerprint = null
);

public record MarkAttendanceDto(
    int UserId,
    string Date,
    string? ClockIn,
    string? ClockOut,
    string Status,
    string? Notes,
    string? DeviceFingerprint = null
);

public record ClockInOutDto(
    int UserId,
    string? Notes,
    string? DeviceFingerprint = null
);

public record AttendanceSummaryDto(
    int TotalEmployees,
    int Present,
    int Late,
    int Absent,
    int Justified
);

public record IngredientDto(
    string MaterialId,
    decimal Quantity,
    decimal UnitCost
);

public record OrderItemDto(
    string Id,
    string ProductId,
    string Name,
    int Quantity,
    decimal Price,
    decimal Labor,
    string Notes,
    List<IngredientDto> Recipe
);

public record PaymentDto(
    string Id,
    decimal Amount,
    string Method,
    string Reference,
    string Date
);

public record OrderHistoryDto(
    int Id,
    string Date,
    string Title,
    string Note
);

public record OrderDto(
    string Id,
    string Number,
    string CreatedAt,
    string Customer,
    string Phone,
    string Email,
    string Recipient,
    string RecipientPhone,
    string Address,
    string Area,
    string DeliveryDate,
    string Time,
    string DeliveryMethod,
    string Priority,
    decimal Discount,
    decimal Shipping,
    bool HasCard,
    string CardMessage,
    string Notes,
    string Status,
    bool Consumed,
    bool IsDirectSale,
    string? DeliveredAt,
    string? ReceivedBy,
    string? DeliveryNote,
    string? FinalArrangementPhotoUrl,
    decimal Total,
    decimal Paid,
    decimal Balance,
    List<OrderItemDto> Items,
    List<PaymentDto> Payments,
    List<OrderHistoryDto> History
);

public record DirectSaleItemDto(
    string ProductId,
    string Name,
    int Quantity,
    decimal Price
);

public record DirectSaleDto(
    string Customer,
    string Phone,
    List<DirectSaleItemDto> Items,
    decimal Discount,
    string PaymentMethod,
    string Reference,
    string Notes
);

public record AddPaymentDto(
    decimal Amount,
    string Method,
    string Reference
);

public record TransitionOrderDto(
    string TargetStatus,
    string? ReceivedBy,
    string? DeliveryNote
);

public record UpdateOrderStatusDto(
    string Status,
    string? Note,
    string? ReceivedBy
);

public record MaterialDto(
    string Id,
    string Name,
    string Category,
    string Unit,
    decimal Stock,
    decimal Minimum,
    decimal Cost,
    string Supplier,
    decimal Reserved,
    decimal Available
);

public record StockMovementDto(
    string Id,
    string MaterialId,
    string MaterialName,
    string Type,
    decimal Quantity,
    decimal Cost,
    string Date,
    string Reason,
    string? OrderId
);

public record ExpenseDto(
    string Id,
    string Category,
    string Description,
    decimal Amount,
    string Date,
    string Method
);

public record FinancialSummaryDto(
    decimal Revenue,
    decimal Cost,
    decimal Expenses,
    decimal Waste,
    decimal Profit,
    decimal Collected,
    int CompletedSales
);

public record CashRegisterSummaryDto(
    string Date,
    decimal OpeningBalance,
    decimal CashSales,
    decimal ElectronicSales,
    decimal TotalSales,
    decimal CashExpenses,
    decimal ExpectedCash,
    int OrderCount
);

public record CloseCashRegisterRequest(
    string Date,
    decimal OpeningBalance,
    decimal ActualCash,
    string CashierName,
    string? Notes
);

public record CashRegisterClosureDto(
    string Id,
    string Date,
    string ClosedAt,
    string CashierName,
    decimal OpeningBalance,
    decimal CashSales,
    decimal ElectronicSales,
    decimal TotalSales,
    decimal TotalExpenses,
    decimal ExpectedCash,
    decimal ActualCash,
    decimal Difference,
    int OrderCount,
    string? Notes
);

public record BackupInfoDto(
    string FileName,
    long FileSizeBytes,
    string FormattedSize,
    DateTime CreatedAt,
    string DownloadUrl
);
