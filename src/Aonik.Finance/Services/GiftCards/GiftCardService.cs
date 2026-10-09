using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.Finance.Entities.GiftCards;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.GiftCards;

internal sealed partial class GiftCardService(
    FinanceDbContext db, ITenantProvider tenantProvider, IClock clock,
    ITenantSettingStore settings, IJournalWriter journals, IDataProtectionProvider protection) : IGiftCardService
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private Guid TenantId => tenantProvider.GetCurrentTenantId();

    public async Task<GiftCardPolicy> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        var raw = await settings.GetTenantValueAsync(GiftCardSettings.Policy, TenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw)) return new();
        GiftCardPolicy policy;
        try
        {
            if (raw.Length > 32768) throw new JsonException();
            policy = JsonSerializer.Deserialize<GiftCardPolicy>(raw, Json) ?? throw new JsonException();
        }
        catch (JsonException) { throw new InvalidStateException("The gift-card issuance policy is unavailable."); }
        if (!policy.Enabled) return new();
        ValidatePolicy(policy);
        await GiftCardAccounting.BindingAsync(db, TenantId, policy.Ledger!, cancellationToken);
        return policy with { Version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(policy)))) };
    }

    public async Task<GiftCardBalance?> GetBalanceAsync(string code, CancellationToken cancellationToken = default)
    {
        var card = await FindCodeAsync(code, cancellationToken);
        return card is null ? null : await BalanceAsync(card, cancellationToken);
    }

    public async Task<GiftCardAuthorization> AuthorizeForCartAsync(string code, Guid cartId, CancellationToken cancellationToken = default)
    {
        if (cartId == Guid.Empty) throw new InvalidStateException("A gift card requires an authorized cart.");
        var card = await FindCodeAsync(code, cancellationToken);
        if (card is null) return new(null, null, "gift_card.invalid");
        var balance = await BalanceAsync(card, cancellationToken);
        var reason = UnavailableReason(card);
        if (reason is not null) return new(null, balance, reason);
        await GiftCardAccounting.BindingAsync(db, TenantId, ReadPolicy(card).Ledger!, cancellationToken);
        return new(GrantProtector(cartId).Protect(card.Id.ToString("N")), balance);
    }

    public async Task<GiftCardFundingQuote> QuoteAsync(GiftCardQuoteRequest request, CancellationToken cancellationToken = default)
    {
        ValidateQuote(request);
        var cardId = ReadGrant(request.PrivateCartGrant, request.CartId);
        var card = cardId is null ? null : await db.GiftCards.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == cardId && !x.IsDeleted, cancellationToken);
        GiftCardFundingQuote Rejected(string reason, decimal max = 0) => new(request.RequestedAmount, max, 0, request.Total, null, reason);
        if (card is null) return Rejected("gift_card.invalid");
        var reason = UnavailableReason(card);
        if (reason is not null) return Rejected(reason);
        var policy = ReadPolicy(card);
        await GiftCardAccounting.BindingAsync(db, TenantId, policy.Ledger!, cancellationToken);
        var balance = await BalanceAsync(card, cancellationToken);
        var max = Math.Min(request.Total, balance.Available);
        if (request.RequestedAmount > max) return Rejected("gift_card.insufficient_balance", max);
        var remainder = request.Total - request.RequestedAmount;
        if (remainder is > 0 and < .30m) return Rejected("gift_card.card_amount_too_small", max);
        var shares = Allocate(request.RequestedAmount, request.Lines, request.TaxTotal);
        var lines = request.Lines.Select((line, index) => line with { GiftFundedValue = shares[index] }).ToArray();
        var tender = new GiftCardTender(request.PrivateCartGrant, request.RequestedAmount, remainder,
            request.TaxTotal, shares[^1], lines);
        return new(request.RequestedAmount, max, request.RequestedAmount, remainder,
            new(request.CartId, policy.Version, policy.Ledger!, policy.Validity!, policy.TermsVersion!, policy.FundingAllocation!, Tender: tender));
    }

    public async Task<GiftCardIssuedInfo?> GetIssuedAsync(GiftCardPurchaseSource source, CancellationToken cancellationToken = default)
    {
        var card = await FindSourceAsync(source, cancellationToken);
        if (card is null) return null;
        await GiftCardAccounting.RequireIssuedAsync(db, TenantId, card, cancellationToken);
        return Info(card);
    }

    public async Task<GiftCardFulfilmentSecret> GetFulfilmentSecretAsync(GiftCardPurchaseSource source, CancellationToken cancellationToken = default)
    {
        var card = await FindSourceAsync(source, cancellationToken) ?? throw new NotFoundException("Issued gift card was not found.");
        await GiftCardAccounting.RequireIssuedAsync(db, TenantId, card, cancellationToken);
        string code;
        try { code = CodeProtector(card.Id).Unprotect(card.ProtectedCode); }
        catch (CryptographicException) { throw new InvalidStateException("The issued gift card is temporarily unavailable."); }
        if (Hash(code) != card.CodeHash) throw new InvalidStateException("The issued gift card is temporarily unavailable.");
        return new(Info(card), code);
    }

    private Task<GiftCard?> FindSourceAsync(GiftCardPurchaseSource source, CancellationToken ct) => db.GiftCards.AsNoTracking()
        .SingleOrDefaultAsync(x => x.TenantId == TenantId && !x.IsDeleted && x.CartId == source.CartId
            && x.OrderId == source.OrderId && x.PaymentIntentId == source.PaymentIntentId
            && x.OrderItemId == source.OrderItemId && x.ItemIndex == source.ItemIndex, ct);

    private async Task<GiftCard?> FindCodeAsync(string code, CancellationToken ct)
    {
        var normalized = NormalizeCode(code);
        if (normalized is null) return null;
        var hash = Hash(normalized);
        return await db.GiftCards.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId && !x.IsDeleted && x.CodeHash == hash, ct);
    }

    private async Task<GiftCardBalance> BalanceAsync(GiftCard card, CancellationToken ct)
    {
        await GiftCardAccounting.RequireIssuedAsync(db, TenantId, card, ct);
        var policy = ReadPolicy(card);
        var invalid = await db.GiftCardOperations.AsNoTracking().Where(x => x.TenantId == TenantId && x.GiftCardId == card.Id)
            .AnyAsync(operation => !db.JournalEntryLines.Any(line => line.TenantId == TenantId
                && line.Id == operation.JournalEntryLineId && line.JournalEntryId == operation.JournalEntryId
                && line.LedgerAccountId == policy.Ledger!.LiabilityAccountId && line.Currency == "GBP" && line.Amount == operation.Amount
                && ((operation.Kind == "Issue" && line.Direction == JournalDirections.Credit)
                    || (operation.Kind == "Redeem" && line.Direction == JournalDirections.Debit))
                && db.JournalEntries.Any(entry => entry.TenantId == TenantId && entry.Id == line.JournalEntryId
                    && entry.Status == "Posted" && entry.LedgerId == policy.Ledger.LedgerId
                    && entry.SourceId == operation.SourceId && entry.SourceType == "GiftCard" + operation.Kind)), ct);
        if (invalid) throw new InvalidStateException("Gift-card ledger evidence is unavailable.");
        var balance = await (from operation in db.GiftCardOperations.AsNoTracking()
            where operation.TenantId == TenantId && operation.GiftCardId == card.Id
            join line in db.JournalEntryLines.AsNoTracking().Where(x => x.TenantId == TenantId)
                on operation.JournalEntryLineId equals line.Id
            select line.Direction == JournalDirections.Credit ? line.Amount : -line.Amount).SumAsync(ct);
        var reserved = await db.GiftCardCheckoutAttempts.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.GiftCardId == card.Id && x.Status == "Reserved").SumAsync(x => x.ReservedAmount, ct);
        return new(card.MaskedCode, card.Currency, balance, reserved,
            UnavailableReason(card) is null ? Math.Max(0, balance - reserved) : 0,
            card.ExpiresAtUtc <= clock.UtcNow ? "Expired" : card.Status, card.ExpiresAtUtc);
    }

    private string? UnavailableReason(GiftCard card) => card.Status != "Active" || card.Currency != "GBP"
        ? "gift_card.unavailable" : card.ExpiresAtUtc <= clock.UtcNow ? "gift_card.expired" : null;
    private IDataProtector GrantProtector(Guid cartId) => protection.CreateProtector("Aonik.Finance.GiftCard.CartGrant.v1", TenantId.ToString("N"), cartId.ToString("N"));
    private IDataProtector CodeProtector(Guid cardId) => protection.CreateProtector("Aonik.Finance.GiftCard.Code.v1", TenantId.ToString("N"), cardId.ToString("N"));
    private Guid? ReadGrant(string grant, Guid cartId)
    {
        if (string.IsNullOrWhiteSpace(grant) || grant.Length > 2048 || cartId == Guid.Empty) return null;
        try { return Guid.TryParseExact(GrantProtector(cartId).Unprotect(grant), "N", out var id) && id != Guid.Empty ? id : null; }
        catch (CryptographicException) { return null; }
    }
    private string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Aonik.GiftCard.v1:" + TenantId.ToString("N") + ":" + code)));
    private static string? NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 64) return null;
        var value = code.Trim().Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();
        return value.Length == 32 && value.All(char.IsAsciiHexDigit) ? value : null;
    }
    private static GiftCardIssuedInfo Info(GiftCard card) => new(card.Id,
        new(card.CartId, card.OrderId, card.PaymentIntentId, card.OrderItemId, card.ItemIndex), card.FaceValue,
        card.Currency, card.MaskedCode, card.IssuedAtUtc, card.ExpiresAtUtc, ReadPolicy(card).TermsVersion!, card.Status);
    internal static GiftCardPolicy ReadPolicy(GiftCard card)
    {
        var policy = JsonSerializer.Deserialize<GiftCardPolicy>(card.PolicySnapshotJson, Json)
            ?? throw new InvalidStateException("The original gift-card policy is unavailable.");
        ValidatePolicy(policy);
        return policy;
    }
    private static void ValidatePolicy(GiftCardPolicy policy)
    {
        if (!policy.Enabled || policy.Currency != "GBP" || !Text(policy.Version, 128) || !Text(policy.TermsVersion, 128)
            || policy.Ledger is null || policy.Validity is null || policy.FundingAllocation != GiftCardSettings.Proportional
            || (policy.Validity.NeverExpires ? policy.Validity.ValidForDays is not null : policy.Validity.ValidForDays is not (>= 1 and <= 36500)))
            throw new InvalidStateException("Gift-card issuance requires explicit GBP accounts, validity, terms and proportional funding policy.");
    }
    private static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max && !value.Any(char.IsControl);
    private static bool Money(decimal value) => value >= 0 && value <= 999999999999999.99m && decimal.Round(value, 2) == value;
    private static bool Share(decimal value) => value >= 0 && value <= 999999999999999.9999m && decimal.Round(value, 4) == value;
    private static string Serialize<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, Json);
        if (json.Length > 262144) throw new InvalidStateException("The gift-card instruction is too large.");
        return json;
    }
    private static GiftCardCheckout Read(GiftCardCheckoutAttempt attempt) => JsonSerializer.Deserialize<GiftCardCheckout>(attempt.SnapshotJson, Json)
        ?? throw new InvalidStateException("The frozen gift-card instruction is unavailable.");
}
