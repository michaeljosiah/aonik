using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Contracts.Services.Partners.Connectors;

public interface IPartnerBillPaymentConnector : IPartnerConnector
{
    Task<IReadOnlyList<BillerCatalogEntry>> GetBillerCatalogAsync(
        BillerCatalogQuery query, CancellationToken cancellationToken = default);

    Task<BillCustomerValidationResult> ValidateCustomerAsync(
        BillCustomerValidationRequest request, CancellationToken cancellationToken = default);

    Task<BillPaymentResult> PayBillAsync(
        BillPaymentInstruction instruction, CancellationToken cancellationToken = default);

    Task<BillPaymentStatusResult> GetBillPaymentStatusAsync(
        PartnerReference reference, CancellationToken cancellationToken = default);
}

public enum BillAmountType { Fixed, Variable }

// Maps eTranzact expectedFields[] / Flutterwave label_name.
public sealed record BillCustomerField(string Key, string Label, bool Required);

public sealed record BillItem(
    string ItemCode, string Name, BillAmountType AmountType,
    Money? FixedAmount, Money? MinAmount, Money? MaxAmount);

// ServiceCategory marks a biller as BillPayment or AirtimeTopup (a telco). The connector still
// routes by biller / item code; the category drives reporting and the persisted record's ServiceCategory.
public sealed record BillerCatalogEntry(
    string BillerCode, string BillerName, string CategoryCode, string CategoryName,
    PartnerServiceCategory ServiceCategory,
    IReadOnlyList<BillCustomerField> CustomerFields, IReadOnlyList<BillItem> Items);

public sealed record BillerCatalogQuery(string? CategoryCode, string? Country, string? Currency);

public sealed record BillCustomerValidationRequest(
    string ClientReference, string BillerCode, string ItemCode, string CustomerId,
    IReadOnlyDictionary<string, string>? Inputs);

// ValidationToken = eTranzact billQueryRef / paymentRef, carried into payment.
public sealed record BillCustomerValidationResult(
    bool IsValid, string? ValidationToken, string? CustomerName,
    IReadOnlyDictionary<string, string>? ResolvedFields,
    Money? OutstandingAmount, RawProviderResponse Raw);

// ServiceCategory is advisory (the connector routes by biller / item code); it is carried so the
// persisted PartnerBillPayment.ServiceCategory is unambiguous - bill payment vs airtime top-up.
public sealed record BillPaymentInstruction(
    string ClientReference, string BillerCode, string ItemCode, string CustomerId,
    Money Amount, string? ValidationToken,
    PartnerServiceCategory? ServiceCategory,
    IReadOnlyDictionary<string, string>? Inputs);

// Token carries e.g. prepaid-electricity vend tokens.
public sealed record BillPaymentResult(
    PartnerReference Reference, PartnerTransactionStatus Status,
    string? Token, RawProviderResponse Raw);

public sealed record BillPaymentStatusResult(
    PartnerReference Reference, PartnerTransactionStatus Status,
    string? Token, RawProviderResponse Raw);
