using System.Globalization;
using System.Text.RegularExpressions;

using Aonik.Finance.Contracts.Models.Remittance;

namespace Aonik.Finance.Services.Remittance;

public static class RemittanceReceiptContent
{
    public static string FileName(RemittanceOrderResponse transfer) =>
        $"transfer-receipt-{Regex.Replace(string.IsNullOrWhiteSpace(transfer.OrderNumber) ? transfer.OrderId.ToString() : transfer.OrderNumber, "[^a-zA-Z0-9_-]", "-")[..Math.Min(100, string.IsNullOrWhiteSpace(transfer.OrderNumber) ? 36 : transfer.OrderNumber.Length)]}.pdf";

    public static string Amount(decimal amount, string currency) => $"{currency} {amount.ToString("#,##0.00##", CultureInfo.InvariantCulture)}";
    public static string Timestamp(DateTime? value) => value.HasValue ? $"{DateTime.SpecifyKind(value.Value, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss} UTC" : "Not recorded";
    public static string Status(RemittanceOrderResponse transfer) => transfer.Status == "Complete" && transfer.TransmissionStatus == "Succeeded" ? "Completed" : transfer.Status;
    public static string StatusDescription(RemittanceOrderResponse transfer) => Status(transfer) == "Completed"
        ? "The transfer is recorded as complete and its payout transmission succeeded."
        : "Receipt of the transfer instruction. This does not confirm recipient payment as completed.";
}
