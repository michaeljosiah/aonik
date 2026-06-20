using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Payments;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Payments;

/// <summary>
/// Spec 007 acceptance: a token-only card vault. No PAN/PCI data is ever stored; methods are
/// tenant- and customer-scoped; the active list aligns with gateway availability; and the setup
/// intent yields a client secret + accepted method types.
/// </summary>
public class PaymentMethodServiceTests
{
    /// <summary>A second registered gateway whose ProviderCode can be toggled out of the available set.</summary>
    private sealed class StubGateway : IPaymentProviderGateway
    {
        public StubGateway(string code) => ProviderCode = code;
        public string ProviderCode { get; }

        public Task<PaymentProviderIntentResult> CreateIntentAsync(PaymentProviderIntentRequest request, CancellationToken ct = default)
            => Task.FromResult(new PaymentProviderIntentResult(ProviderCode, "ref", "Pending", null, null));

        public Task<PaymentProviderSetupIntentResult> CreateSetupIntentAsync(PaymentProviderSetupIntentRequest request, CancellationToken ct = default)
            => Task.FromResult(new PaymentProviderSetupIntentResult(ProviderCode, "seti_x", "seti_x_secret", ["card"], "cus_x"));
    }

    private static FinanceDbContext CreateDbContext(Guid tenantId, Guid userId, string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"PaymentMethodTests_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(options, new TestTenantProvider(tenantId), new TestCurrentUserProvider(userId));
    }

    private static PaymentMethodService CreateService(
        FinanceDbContext context, Guid tenantId, Guid userId, params IPaymentProviderGateway[] gateways)
        => new(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            gateways.Length == 0 ? [new StripeSimulatedPaymentProviderGateway()] : gateways);

    private static Guid SeedCustomer(FinanceDbContext context, Guid tenantId, Guid userId)
    {
        var partyId = Guid.NewGuid();
        context.UserParties.Add(new UserPartyReadModel
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId,
        });
        context.SaveChanges();
        return partyId;
    }

    private static SavePaymentMethodRequest CardRequest(
        string token = "pm_visa_001", string? brand = "VISA", string? last4 = "4242",
        bool makeDefault = false, string? provider = null)
        => new(
            ProviderToken: token,
            Provider: provider,
            Type: "card",
            Brand: brand,
            Last4: last4,
            ExpiryMonth: 12,
            ExpiryYear: 2030,
            Label: "Personal Visa",
            MakeDefault: makeDefault);

    [Fact]
    public async Task CreateSetupIntentAsync_Should_ReturnClientSecretAndTypes_When_CustomerResolved()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId, userId);
        SeedCustomer(context, tenantId, userId);
        var service = CreateService(context, tenantId, userId);

        var result = await service.CreateSetupIntentAsync();

        result.Provider.Should().Be("Stripe");
        result.ClientSecret.Should().NotBeNullOrWhiteSpace();
        result.PaymentMethodTypes.Should().Contain("card");
        result.SetupIntentReference.Should().StartWith("seti_");
    }

    [Fact]
    public async Task SaveAsync_Should_VaultTokenAndMaskedMetadata_When_ValidToken()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId, userId);
        var partyId = SeedCustomer(context, tenantId, userId);
        var service = CreateService(context, tenantId, userId);

        var response = await service.SaveAsync(CardRequest());

        response.Brand.Should().Be("visa");
        response.Last4.Should().Be("4242");
        response.IsDefault.Should().BeTrue("the first saved card is the default");

        // The persisted row holds the opaque token + owner — and no PAN.
        var stored = await context.PaymentMethods.SingleAsync();
        stored.ProviderToken.Should().Be("pm_visa_001");
        stored.CustomerPartyId.Should().Be(partyId);
        stored.Last4.Should().Be("4242");
    }

    [Fact]
    public async Task SaveAsync_Should_NeverExposeTokenOnResponse_When_Saved()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId, userId);
        SeedCustomer(context, tenantId, userId);
        var service = CreateService(context, tenantId, userId);

        var response = await service.SaveAsync(CardRequest());

        // PaymentMethodResponse carries only masked display fields — the type has no token member.
        typeof(PaymentMethodResponse).GetProperties()
            .Select(p => p.Name)
            .Should().NotContain(["ProviderToken", "Token", "Pan", "CardNumber"]);
    }

    [Theory]
    [InlineData("4242424242424242")]   // 16-digit PAN
    [InlineData("4242 4242 4242 4242")] // spaced PAN
    [InlineData("4111-1111-1111-1111")] // hyphenated PAN
    [InlineData("4000123412341234567")] // 19-digit PAN
    public async Task SaveAsync_Should_RejectAndPersistNothing_When_TokenLooksLikeRawPan(string pan)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId, userId);
        SeedCustomer(context, tenantId, userId);
        var service = CreateService(context, tenantId, userId);

        var act = () => service.SaveAsync(CardRequest(token: pan));

        await act.Should().ThrowAsync<ArgumentException>();
        (await context.PaymentMethods.CountAsync()).Should().Be(0, "a rejected PAN must write nothing");
    }

    [Theory]
    [InlineData("4111 1111 1111 1111", null)]                  // standalone PAN in Label
    [InlineData("my card 4242424242424242 spare", null)]       // PAN embedded in free-form Label text
    [InlineData(null, "4111-1111-1111-1111")]                  // PAN in the Provider field
    public async Task SaveAsync_Should_RejectAndPersistNothing_When_PanInFreeFormOrProviderField(
        string? label, string? provider)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId, userId);
        SeedCustomer(context, tenantId, userId);
        var service = CreateService(context, tenantId, userId);

        // A valid token, but a PAN hidden in a display/provider field — must still be rejected (no PCI in ANY field).
        var request = CardRequest();
        if (label is not null) request = request with { Label = label };
        if (provider is not null) request = request with { Provider = provider };

        var act = () => service.SaveAsync(request);

        await act.Should().ThrowAsync<ArgumentException>();
        (await context.PaymentMethods.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SaveAsync_Should_UpdateInPlace_When_SameProviderTokenResaved()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId, userId);
        SeedCustomer(context, tenantId, userId);
        var service = CreateService(context, tenantId, userId);

        await service.SaveAsync(CardRequest(token: "pm_dup", last4: "4242"));
        await service.SaveAsync(CardRequest(token: "pm_dup", last4: "4242", brand: "MASTERCARD"));

        var methods = await context.PaymentMethods.ToListAsync();
        methods.Should().HaveCount(1, "re-saving the same token is idempotent");
        methods[0].Brand.Should().Be("mastercard");
    }

    [Fact]
    public async Task SaveAsync_Should_MoveDefault_When_MakeDefaultOnSecondCard()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId, userId);
        SeedCustomer(context, tenantId, userId);
        var service = CreateService(context, tenantId, userId);

        var first = await service.SaveAsync(CardRequest(token: "pm_1"));
        var second = await service.SaveAsync(CardRequest(token: "pm_2", makeDefault: true));

        first.IsDefault.Should().BeTrue();
        second.IsDefault.Should().BeTrue();

        var list = await service.ListAsync();
        list.Count(m => m.IsDefault).Should().Be(1, "exactly one default per customer");
        list.Single(m => m.IsDefault).Id.Should().Be(second.Id);
    }

    [Fact]
    public async Task ListAsync_Should_ReturnMaskedMethodsForCustomerOnly()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId, userId);
        SeedCustomer(context, tenantId, userId);
        var service = CreateService(context, tenantId, userId);

        await service.SaveAsync(CardRequest(token: "pm_a"));
        await service.SaveAsync(CardRequest(token: "pm_b"));

        var list = await service.ListAsync();
        list.Should().HaveCount(2);
        list.Should().OnlyContain(m => m.Last4 == "4242" && m.Brand == "visa");
    }

    [Fact]
    public async Task GetAsync_Should_ReturnNull_When_MethodBelongsToAnotherUser()
    {
        var tenantId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        using var context = CreateDbContext(tenantId, ownerUserId);
        SeedCustomer(context, tenantId, ownerUserId);
        SeedCustomer(context, tenantId, otherUserId);

        var ownerService = CreateService(context, tenantId, ownerUserId);
        var saved = await ownerService.SaveAsync(CardRequest());

        // A different user in the same tenant must not see the owner's method.
        var otherService = CreateService(context, tenantId, otherUserId);
        (await otherService.GetAsync(saved.Id)).Should().BeNull();
        (await otherService.ListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_Should_RemoveOwnedMethodAndPromoteNextDefault()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId, userId);
        SeedCustomer(context, tenantId, userId);
        var service = CreateService(context, tenantId, userId);

        var first = await service.SaveAsync(CardRequest(token: "pm_1"));   // default
        await service.SaveAsync(CardRequest(token: "pm_2"));

        var removed = await service.DeleteAsync(first.Id);

        removed.Should().BeTrue();
        var list = await service.ListAsync();
        list.Should().HaveCount(1);
        list.Single().IsDefault.Should().BeTrue("a remaining card is promoted to default");
    }

    [Fact]
    public async Task DeleteAsync_Should_ReturnFalse_When_MethodBelongsToAnotherUser()
    {
        var tenantId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        using var context = CreateDbContext(tenantId, ownerUserId);
        SeedCustomer(context, tenantId, ownerUserId);
        SeedCustomer(context, tenantId, otherUserId);

        var saved = await CreateService(context, tenantId, ownerUserId).SaveAsync(CardRequest());

        var otherService = CreateService(context, tenantId, otherUserId);
        (await otherService.DeleteAsync(saved.Id)).Should().BeFalse();
        (await context.PaymentMethods.CountAsync()).Should().Be(1, "another user's delete must not remove it");
    }

    [Fact]
    public async Task ListActiveAsync_Should_ExcludeMethodsOnUnavailableProviders()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId, userId);
        SeedCustomer(context, tenantId, userId);

        // Two providers available at save time.
        var saveService = CreateService(context, tenantId, userId,
            new StripeSimulatedPaymentProviderGateway(), new StubGateway("LegacyRail"));
        await saveService.SaveAsync(CardRequest(token: "pm_stripe", provider: "Stripe"));
        await saveService.SaveAsync(CardRequest(token: "pm_legacy", provider: "LegacyRail"));

        (await saveService.ListAsync()).Should().HaveCount(2);

        // Now only Stripe is registered: the LegacyRail method drops off the active list.
        var stripeOnlyService = CreateService(context, tenantId, userId, new StripeSimulatedPaymentProviderGateway());
        var active = await stripeOnlyService.ListActiveAsync();

        active.Should().HaveCount(1);
        active.Single().Provider.Should().Be("Stripe");
    }

    [Fact]
    public async Task ListAsync_Should_ReturnEmpty_When_DifferentTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sharedDb = $"PaymentMethodTenantIsolation_{Guid.NewGuid()}";

        using var context = CreateDbContext(tenantA, userId, sharedDb);
        SeedCustomer(context, tenantA, userId);
        await CreateService(context, tenantA, userId).SaveAsync(CardRequest());

        // Same physical store, same user id, different tenant context — the tenant query filter isolates.
        using var contextB = CreateDbContext(tenantB, userId, sharedDb);
        var list = await CreateService(contextB, tenantB, userId).ListAsync();
        list.Should().BeEmpty();
    }
}
