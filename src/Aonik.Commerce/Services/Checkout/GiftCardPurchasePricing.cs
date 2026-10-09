using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.GiftCards;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Settings;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Checkout;

internal sealed class GiftCardPurchasePricing(CommerceDbContext db, ITenantProvider tenantProvider,
    ITenantSettingStore settings, IGiftCardService gifts, IClock clock)
{
    internal const string SettingName = "Commerce.Storefront.GiftCards";
    internal const string PostageItemType = "GiftCardPostage";
    internal const string GreetingItemType = "GiftCardGreeting";
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<(GiftCardStorefrontOptions Store, GiftCardPolicy Finance, string Version)> PolicyAsync(CancellationToken ct = default)
    {
        var raw = await settings.GetTenantValueAsync(SettingName, tenantProvider.GetCurrentTenantId(), ct);
        var store = raw is null ? new GiftCardStorefrontOptions() : JsonSerializer.Deserialize<GiftCardStorefrontOptions>(raw, Json)
            ?? throw new StorefrontValidationException("Gift-card configuration is invalid.");
        var finance = await gifts.GetPolicyAsync(ct);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { store, finance }, Json))));
        return (store, finance, fingerprint);
    }

    public async Task<GiftCardOptionsDto> OptionsAsync(CancellationToken ct = default)
    {
        var (s, f, version) = await PolicyAsync(ct);
        return new(s.Enabled && f.Enabled, version, s.ProductVariantId, s.Values ?? [], s.CustomMinimum,
            s.CustomMaximum, s.DeliveryMethods ?? [], s.Postage, s.GreetingCardPrice, s.Timezone,
            s.EmailSendTime, s.PostingDays ?? [], f.TermsVersion, f.Validity?.NeverExpires ?? false,
            f.Validity?.ValidForDays, s.TaxTreatment);
    }

    public async Task RejectOrdinaryVariantAsync(Guid variantId, CancellationToken ct = default)
    {
        var raw = await settings.GetTenantValueAsync(SettingName, tenantProvider.GetCurrentTenantId(), ct);
        if (raw == null) return;
        var store = JsonSerializer.Deserialize<GiftCardStorefrontOptions>(raw, Json);
        if (store?.ProductVariantId == variantId)
            throw new StorefrontValidationException("Use the gift-card purchase route for this product.");
    }

    public async Task<GiftCardPurchaseDto> SelectAsync(Cart cart, GiftCardPurchaseSelection selection, CancellationToken ct = default)
    {
        var (store, finance, version) = await PolicyAsync(ct);
        ValidatePolicy(store, finance, version, selection, cart);
        var normalized = selection with
        {
            RecipientName = CartDraftData.Text(selection.RecipientName, 201, "RecipientName") ?? "",
            RecipientEmail = CartDraftData.Text(selection.RecipientEmail, 254, "RecipientEmail"),
            RecipientPhone = CartDraftData.Text(selection.RecipientPhone, 32, "RecipientPhone"),
            SenderName = CartDraftData.Text(selection.SenderName, 201, "SenderName"),
            Message = CartDraftData.Text(selection.Message, 1000, "Message", true),
            PostalAddress = CartDraftData.Normalize(new CartCheckoutDraftDto(Address: selection.PostalAddress)).Address
        };
        if (normalized.RecipientName.Length == 0) throw new StorefrontValidationException("Enter the gift recipient's name.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(store.Timezone!);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, zone));
        DateTime? sendAt = null;
        if (normalized.DeliveryMethod == "Email")
        {
            if (normalized.RecipientEmail is not { } email || !System.Net.Mail.MailAddress.TryCreate(email, out var parsed)
                || parsed.Address != email || normalized.SendDate is not { } send || send < today || send > today.AddDays(365))
                throw new StorefrontValidationException("Enter a recipient email and a send date within the next year.");
            var local = send.ToDateTime(store.EmailSendTime!.Value, DateTimeKind.Unspecified);
            if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
                throw new StorefrontValidationException("Choose another send date; the configured send time is ambiguous.");
            sendAt = TimeZoneInfo.ConvertTimeToUtc(local, zone);
            if (sendAt < clock.UtcNow) sendAt = clock.UtcNow;
            normalized = normalized with { PostingDate = null, PostalAddress = null, RecipientPhone = null, IncludeGreetingCard = false };
        }
        else if (normalized.DeliveryMethod == "Post")
        {
            if (normalized.PostingDate is not { } post || post < today || post > today.AddDays(365)
                || store.PostingDays?.Contains(post.DayOfWeek) != true)
                throw new StorefrontValidationException("Choose an available posting date within the next year.");
            var address = normalized.PostalAddress ?? throw new StorefrontValidationException("Enter the postal delivery address.");
            if (address.CountryCode != "GB") throw new StorefrontValidationException("Gift-card post currently requires a GB address.");
            CheckoutContactValidation.ValidateAddress(address, normalized.RecipientPhone);
            normalized = normalized with { SendDate = null, RecipientEmail = null };
        }
        else
        {
            if (cart.BoxBundleProductId is null) throw new StorefrontValidationException("In-box delivery requires a food box.");
            normalized = normalized with { SendDate = null, RecipientEmail = null, PostingDate = null, PostalAddress = null, RecipientPhone = null };
        }
        await VariantAsync(store.ProductVariantId, ct);
        return new(normalized, sendAt, normalized.DeliveryMethod == "Post" ? store.Postage : 0m,
            normalized.IncludeGreetingCard ? store.GreetingCardPrice : 0m);
    }

    public async Task<ProductVariant> VariantAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = tenantProvider.GetCurrentTenantId();
        var variant = await db.ProductVariants.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == id, ct);
        if (variant == null || !variant.IsActive || !await db.Products.AsNoTracking().AnyAsync(x => x.TenantId == tenant
            && x.Id == variant.ProductId && x.Status == ProductStatuses.Active && !x.IsPlaceholder && x.Kind != ProductKinds.Bundle, ct))
            throw new StorefrontValidationException("The gift-card product is not available.");
        return variant;
    }

    public async Task<GiftCardPurchaseQuote?> AppendAsync(Cart cart, List<OrderItemCommand> items, CancellationToken ct = default)
    {
        var purchase = Read(cart);
        var lines = cart.Items.Where(x => !x.IsDeleted && x.LineKind == CartLineKinds.GiftCardValue).ToList();
        if (purchase == null)
        {
            if (lines.Count != 0) throw new StorefrontValidationException("The gift-card selection is missing.");
            return null;
        }
        var (store, finance, version) = await PolicyAsync(ct);
        ValidatePolicy(store, finance, version, purchase.Selection, cart);
        var variant = await VariantAsync(store.ProductVariantId, ct);
        if (lines.Count != 1 || lines[0].ProductVariantId != variant.Id || lines[0].Quantity != 1m
            || lines[0].UnitPriceSnapshot != purchase.Selection.FaceValue
            || purchase.Postage != (purchase.Selection.DeliveryMethod == "Post" ? store.Postage : 0m)
            || purchase.GreetingCardPrice != (purchase.Selection.IncludeGreetingCard ? store.GreetingCardPrice : 0m))
            throw new StorefrontValidationException("Review the gift-card selection and current price.");
        var index = items.Count == 0 ? 0 : items.Max(x => x.ItemIndex) + 1;
        var s = purchase.Selection;
        items.Add(new(CartLineKinds.GiftCardValue, index, s.FaceValue, cart.Currency, Quantity: 1m,
            UnitPrice: s.FaceValue, ProductId: variant.Id, Sku: variant.Sku, NameSnapshot: variant.Name));
        if (purchase.Postage > 0m) items.Add(new(PostageItemType, index + 1, purchase.Postage, cart.Currency,
            Quantity: 1m, UnitPrice: purchase.Postage, NameSnapshot: "Gift-card postage"));
        if (purchase.GreetingCardPrice > 0m) items.Add(new(GreetingItemType, items.Max(x => x.ItemIndex) + 1,
            purchase.GreetingCardPrice, cart.Currency, Quantity: 1m, UnitPrice: purchase.GreetingCardPrice, NameSnapshot: "Gift-card greeting card"));
        return new(new GiftCardCheckout(cart.Id, finance.Version, finance.Ledger!, finance.Validity!, finance.TermsVersion!,
            finance.FundingAllocation!, new GiftCardPurchase(index, Guid.Empty, s.FaceValue)),
            new(index, Guid.Empty, s.FaceValue, cart.Currency, s.DeliveryMethod, s.RecipientName, s.RecipientEmail,
                purchase.SendAtUtc, s.PostingDate, s.PostalAddress, s.RecipientPhone, s.Message, s.SenderName, s.IncludeGreetingCard));
    }

    public static GiftCardPurchaseDto? Read(Cart cart) => cart.GiftCardPurchaseJson == null ? null
        : JsonSerializer.Deserialize<GiftCardPurchaseDto>(cart.GiftCardPurchaseJson, Json);

    private static void ValidatePolicy(GiftCardStorefrontOptions store, GiftCardPolicy finance, string version,
        GiftCardPurchaseSelection s, Cart cart)
    {
        if (!store.Enabled || !finance.Enabled || finance.Ledger == null || finance.Validity == null
            || string.IsNullOrWhiteSpace(store.Version) || string.IsNullOrWhiteSpace(finance.Version)
            || string.IsNullOrWhiteSpace(finance.TermsVersion) || finance.FundingAllocation != GiftCardSettings.Proportional
            || store.TaxTreatment != "ExcludeFaceValue" || string.IsNullOrWhiteSpace(store.Timezone)
            || store.EmailSendTime == null || store.ProductVariantId == Guid.Empty
            || cart.Currency != "GBP" || finance.Currency != "GBP" || store.Postage < 0m || store.GreetingCardPrice < 0m
            || decimal.Round(store.Postage, 2) != store.Postage || decimal.Round(store.GreetingCardPrice, 2) != store.GreetingCardPrice)
            throw new StorefrontValidationException("Gift-card purchasing is not configured.");
        if (s.AcceptedVersion != version) throw new StorefrontValidationException("Gift-card terms or prices changed. Review and accept the current options.");
        if (s.FaceValue <= 0m || s.FaceValue > 999999.99m || decimal.Round(s.FaceValue, 2) != s.FaceValue
            || !(store.Values?.Contains(s.FaceValue) == true || store.CustomMinimum is { } min && min > 0m
                && store.CustomMaximum is { } max && max >= min && s.FaceValue >= min && s.FaceValue <= max))
            throw new StorefrontValidationException("Choose an available gift-card value.");
        if (s.DeliveryMethod is not ("Email" or "Post" or "InFoodBox") || store.DeliveryMethods?.Contains(s.DeliveryMethod) != true)
            throw new StorefrontValidationException("Choose an available gift-card delivery method.");
        if (s.DeliveryMethod == "InFoodBox" && cart.BoxBundleProductId == null)
            throw new StorefrontValidationException("In-box delivery requires a food box.");
        if (cart.GiftCardTenderJson != null) throw new StorefrontValidationException("A gift card cannot fund a gift-card purchase.");
    }
}

internal sealed record GiftCardPurchaseQuote(GiftCardCheckout Checkout, GiftCardPurchaseSnapshot Delivery);
