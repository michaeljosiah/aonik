using System.Text.Json;

using Aonik.Finance.Contracts.Models.Accounts;
using Aonik.Finance.Contracts.Services.PersonalFinance;

namespace Aonik.Finance.Services.Accounts.Linking;

/// <summary>
/// Pure static helpers shared across the account-linking pipeline:
/// input validation, value normalization, and provider-state classification.
/// No DI dependencies — every method is data-in/data-out.
/// </summary>
internal static class AccountLinkingNormalization
{
    public static void ValidateRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }
    }

    public static string NormalizeMode(string? mode)
    {
        var normalized = string.IsNullOrWhiteSpace(mode) ? "connect" : mode.Trim().ToLowerInvariant();

        return normalized switch
        {
            "connect" => "connect",
            "update" => "update",
            _ => throw new ArgumentException("Mode must be either 'connect' or 'update'.", nameof(mode))
        };
    }

    public static string NormalizeAccountType(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();

        return normalized switch
        {
            "bank" => "BankAccount",
            "depository" => "BankAccount",
            "credit" => "CreditCard",
            "creditcard" => "CreditCard",
            "credit_card" => "CreditCard",
            "loan" => "Loan",
            "investment" => "Investment",
            _ => value.Trim()
        };
    }

    public static string? TrimNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string? LimitText(string? value, int maxLength)
    {
        var normalized = TrimNullable(value);
        return normalized == null ? null
            : normalized.Length <= maxLength ? normalized
            : normalized[..maxLength];
    }

    public static string? NormalizeLast4(string? value)
    {
        var normalized = TrimNullable(value);
        if (normalized == null)
        {
            return null;
        }

        return normalized.Length <= 4 ? normalized : normalized[^4..];
    }

    public static string NormalizeWebhookValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    public static string DetermineConnectionStatus(AccountLinkProviderExchangeResult providerState)
    {
        return !string.IsNullOrWhiteSpace(DetermineConnectionError(providerState))
            ? "ActionRequired"
            : "Connected";
    }

    public static string? DetermineConnectionError(AccountLinkProviderExchangeResult providerState)
    {
        if (!string.IsNullOrWhiteSpace(providerState.LastError))
        {
            return providerState.LastError;
        }

        if (string.Equals(providerState.ConsentStatus, "ActionRequired", StringComparison.OrdinalIgnoreCase))
        {
            return "Reconnect required to continue syncing this account link.";
        }

        return null;
    }

    public static string BuildAccountMetadataJson(CreateAccountRequest request)
    {
        var metadata = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(request.Name))
            metadata["name"] = request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.Currency))
            metadata["currency"] = request.Currency.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(request.InstitutionName))
            metadata["institutionName"] = request.InstitutionName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Notes))
            metadata["notes"] = request.Notes.Trim();

        return JsonSerializer.Serialize(metadata);
    }
}
