using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Services.GiftCards;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Payments;

namespace Aonik.Finance.Services.Payments;

internal sealed record RefundAllocation(string ComponentId, Guid? OrderItemId, decimal Amount,
    bool CompletesComponent, decimal GiftWeight, long EarnedPointsToReverse, long RedeemedPointsToRestore);
internal sealed record RefundCashLedger(Guid ReceiptId, Guid CaptureJournalId, Guid LedgerId,
    Guid CashAccountId, Guid ClearingAccountId, decimal CapturedAmount);

/// <summary>Immutable server facts admitted before any provider request. Not an API model.</summary>
internal sealed record RefundSnapshot(CheckoutRefundSource Source, RefundRequest Request, Guid? ActorId,
    IReadOnlyList<RefundAllocation> Components, decimal CashAmount, decimal GiftAmount,
    RefundCashLedger? CashLedger, PaymentProviderRefundRequest? Provider,
    GiftCardRefundInstruction? Gift, LoyaltyRefund? Loyalty)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { RespectRequiredConstructorParameters = true };
    public string Serialize()
    {
        var value = JsonSerializer.Serialize(this, Json);
        if (value.Length > 262144) throw new InvalidStateException("The refund request is too large.");
        return value;
    }
    public static RefundSnapshot Read(Refund refund) =>
        JsonSerializer.Deserialize<RefundSnapshot>(refund.RequestSnapshotJson
            ?? throw new InvalidStateException("The original refund facts are unavailable."), Json)
        ?? throw new InvalidStateException("The original refund facts are invalid.");
    public static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
}
