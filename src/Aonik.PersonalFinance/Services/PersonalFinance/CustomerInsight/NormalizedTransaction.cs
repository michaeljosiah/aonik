namespace Aonik.Finance.Services.PersonalFinance.CustomerInsight;

/// <summary>
/// Internal projection of <see cref="Aonik.Finance.Entities.PersonalFinance.PersonalTransaction"/>
/// used by every customer-insight builder. The shape is denormalised so all builders
/// can work without re-resolving merchant/account/category every time.
/// </summary>
internal sealed record NormalizedTransaction(
    Guid Id,
    Guid? PersonalAccountId,
    string AccountName,
    DateTime OccurredAtUtc,
    decimal Amount,
    string Currency,
    string MerchantDisplay,
    string MerchantKey,
    string Category,
    string? SubCategory,
    string NormalizedKind,
    string SourceDisplay,
    string SourceKey,
    bool IsConfirmedTransfer,
    bool IsIncome,
    bool IsExpense);
