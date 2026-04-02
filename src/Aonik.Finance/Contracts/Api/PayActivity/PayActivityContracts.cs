namespace Aonik.Finance.Contracts.Api.PayActivity;

/// <summary>
/// Response for the pay activity summary endpoint (GET /payments/activity).
/// Shape matches the mobile app's LivePayActivityRepository expectations.
/// </summary>
public record PayActivitySummaryResponse(
    IReadOnlyList<PayActivityTransactionDto> Transactions);

/// <summary>
/// A single pay activity row shown in the mobile Pay dashboard and activity list.
/// </summary>
public record PayActivityTransactionDto(
    string Id,
    string Title,
    string Subtitle,
    string AmountLabel,
    string Status,
    string Type,
    string DateGroupLabel);

/// <summary>
/// Full transaction detail for the mobile transaction details screen.
/// </summary>
public record PayActivityTransactionDetailResponse(
    string Id,
    string Status,
    string StatusDescription,
    string AmountLabel,
    string FeeLabel,
    string TotalLabel,
    PayActivityRecipientDto Recipient,
    string OrderId,
    string PaymentIntentId,
    string ProviderReference,
    string Reference);

/// <summary>
/// Recipient details shown on the mobile transaction details screen.
/// </summary>
public record PayActivityRecipientDto(
    string Name,
    string Initials,
    string BankName,
    string MaskedAccountNumber,
    string Country);
