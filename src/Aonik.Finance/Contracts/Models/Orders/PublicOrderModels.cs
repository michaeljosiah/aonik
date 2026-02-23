namespace Aonik.Finance.Contracts.Models.Orders;

public record CreateGuestBillPaymentDraftRequest(
    Guid BillerId,
    Guid ServiceId,
    string ServiceCode,
    string ServiceName,
    string? BillerName,
    string CountryCode,
    string Currency,
    Dictionary<string, string> ServiceFieldValues,
    bool IsValidated,
    DateTimeOffset CapturedAt,
    string? ValidationMode,
    string? AccountHolderName,
    decimal? RequestedAmount,
    string? Channel = "Payabo");

public record GuestBillPaymentDraftResponse(
    Guid OrderId,
    string Status,
    DateTime CreatedAt);

public record GuestBillPaymentDraftDetailResponse(
    Guid OrderId,
    string Status,
    DateTime CreatedAt,
    string CountryCode,
    string Currency,
    Guid BillerId,
    string? BillerName,
    Guid ServiceId,
    string ServiceCode,
    string ServiceName,
    Dictionary<string, string> ServiceFieldValues,
    bool IsValidated,
    DateTimeOffset CapturedAt,
    string? ValidationMode,
    string? AccountHolderName,
    decimal? RequestedAmount,
    string Channel);
