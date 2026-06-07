using System.Text.Json;

using Aonik.Finance.Entities;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Orders;
using Aonik.SharedKernel.Abstractions;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Aonik.Application.Tests.Finance.Orders;

/// <summary>
/// Tests for <see cref="OrderService"/> order-to-response mapping, guarding finding H4:
/// <c>MapOrderAsync</c> must batch-load every party an order references (the payer plus each
/// line item's receiver) in a single round-trip and resolve every display name from the
/// resulting dictionary. The InMemory provider can't assert query counts, so these tests lock
/// in the observable contract — all receiver names and the payer name resolve correctly across
/// a multi-item, multi-party order — which would regress if the dictionary wiring broke.
/// </summary>
public class OrderServiceMappingTests
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"OrderMappingTests_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(
            options,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider());
    }

    private static OrderService CreateService(FinanceDbContext context, Guid tenantId)
    {
        return new OrderService(
            context,
            new TestTenantProvider(tenantId),
            Mock.Of<IPartyService>(),
            Mock.Of<IComplianceService>(),
            Mock.Of<IAuditLogWriter>(),
            Mock.Of<IClock>(clock => clock.UtcNow == DateTime.UtcNow),
            new TestCurrentUserProvider(),
            NullLogger<OrderService>.Instance);
    }

    private static PartyReadModel SeedParty(Guid tenantId, string displayName)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DisplayName = displayName,
            Status = "Active"
        };

    private static OrderItem SeedItem(Guid tenantId, Guid orderId, int index, Guid receiverPartyId, string billerName)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            ItemType = "BillPayment",
            ItemIndex = index,
            Status = "Valid",
            ReceiverPartyId = receiverPartyId,
            AmountIn = 100m,
            CurrencyIn = "USD",
            AmountOut = 100m,
            CurrencyOut = "USD",
            FeesTotal = 0m,
            DetailsJson = BuildDetailsJson(receiverPartyId, billerName)
        };

    // Serialize a fully-populated camelCase payload matching the (private) BillPaymentItemDetails
    // record shape so DeserializeDetails reconstructs it cleanly regardless of which constructor
    // System.Text.Json selects.
    private static string BuildDetailsJson(Guid receiverPartyId, string billerName)
        => JsonSerializer.Serialize(
            new
            {
                billerId = Guid.NewGuid(),
                billerName,
                serviceId = Guid.NewGuid(),
                serviceCode = "SVC",
                serviceName = "Service",
                serviceFieldValues = new Dictionary<string, string>(),
                payerPartyId = Guid.Empty,
                receiverPartyId,
                relationshipTypeCode = (string?)null,
                purposeCode = (string?)null,
                notes = (string?)null,
                pricingSnapshot = new
                {
                    pricingQuoteId = Guid.Empty,
                    fxRateId = Guid.Empty,
                    exchangeRate = 1.0m,
                    rateMarkup = 0m,
                    pricingPolicyId = Guid.Empty,
                    pricingPolicyVersion = "v1",
                    rateTimestamp = DateTime.UtcNow,
                    quoteTimestamp = DateTime.UtcNow,
                    quoteExpiresAt = (DateTime?)null,
                    feeBreakdown = Array.Empty<object>()
                }
            },
            CamelCase);

    [Fact]
    public async Task GetOrderAsync_Should_ResolveEveryReceiverNameAndPayerName_FromBatchLoad()
    {
        // Arrange — one order, two line items pointing at two distinct receivers, plus a payer.
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var payer = SeedParty(tenantId, "John Payer");
        var receiverOne = SeedParty(tenantId, "Acme Utilities");
        var receiverTwo = SeedParty(tenantId, "Metro Water");
        context.Parties.AddRange(payer, receiverOne, receiverTwo);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderType = "BillPayment",
            Status = "Draft",
            PayerPartyId = payer.Id,
            CurrencyIn = "USD",
            AmountIn = 200m
        };
        order.Items.Add(SeedItem(tenantId, order.Id, 0, receiverOne.Id, "Acme Bill"));
        order.Items.Add(SeedItem(tenantId, order.Id, 1, receiverTwo.Id, "Metro Bill"));
        context.Orders.Add(order);

        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId);

        // Act
        var result = await service.GetOrderAsync(order.Id);

        // Assert — payer and both receivers resolve to their display names.
        result.PayerPartyId.Should().Be(payer.Id);
        result.PayerName.Should().Be("John Payer");
        result.Items.Should().HaveCount(2);

        var firstItem = result.Items.Single(item => item.ItemIndex == 0);
        firstItem.ReceiverPartyId.Should().Be(receiverOne.Id);
        firstItem.ReceiverName.Should().Be("Acme Utilities");

        var secondItem = result.Items.Single(item => item.ItemIndex == 1);
        secondItem.ReceiverPartyId.Should().Be(receiverTwo.Id);
        secondItem.ReceiverName.Should().Be("Metro Water");
    }

    [Fact]
    public async Task GetOrderAsync_Should_ReturnEmptyReceiverName_When_PartyNotFound()
    {
        // Arrange — the second item references a receiver that was never persisted, so the batch
        // load can't resolve it. The mapping must degrade gracefully to an empty name rather than
        // throw, while still resolving the parties that do exist.
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var payer = SeedParty(tenantId, "Jane Payer");
        var receiverOne = SeedParty(tenantId, "Known Receiver");
        context.Parties.AddRange(payer, receiverOne);

        var missingReceiverId = Guid.NewGuid();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderType = "BillPayment",
            Status = "Draft",
            PayerPartyId = payer.Id,
            CurrencyIn = "USD",
            AmountIn = 200m
        };
        order.Items.Add(SeedItem(tenantId, order.Id, 0, receiverOne.Id, "Known Bill"));
        order.Items.Add(SeedItem(tenantId, order.Id, 1, missingReceiverId, "Missing Bill"));
        context.Orders.Add(order);

        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId);

        // Act
        var result = await service.GetOrderAsync(order.Id);

        // Assert
        result.PayerName.Should().Be("Jane Payer");
        result.Items.Single(item => item.ItemIndex == 0).ReceiverName.Should().Be("Known Receiver");

        var unresolved = result.Items.Single(item => item.ItemIndex == 1);
        unresolved.ReceiverPartyId.Should().Be(missingReceiverId);
        unresolved.ReceiverName.Should().BeEmpty();
    }
}
