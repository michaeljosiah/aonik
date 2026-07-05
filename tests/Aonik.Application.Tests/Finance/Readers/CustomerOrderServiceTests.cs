using Aonik.Finance.Contracts.Models.Orders;
using Aonik.Finance.Services.Finance.Readers;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ordering;
using FluentAssertions;
using Moq;
using FinanceOrderService = Aonik.Finance.Contracts.Services.Orders.IOrderService;

namespace Aonik.Application.Tests.Finance.Readers;

/// <summary>
/// Tests for <see cref="CustomerOrderService"/> — the customer-facing, party-scoped order
/// projection over Finance's <c>IOrderService</c> that lets PersonalFinance's Simi tools read and
/// cancel a customer's own orders through a SharedKernel contract (Spec 027 S-Contracts / #118).
/// The ownership guards are the security-critical surface.
/// </summary>
public class CustomerOrderServiceTests
{
    private static readonly Guid OwnerParty = Guid.NewGuid();
    private static readonly Guid OtherParty = Guid.NewGuid();

    private readonly Mock<FinanceOrderService> _orderService = new(MockBehavior.Strict);

    private CustomerOrderService CreateSut() => new(_orderService.Object);

    // ── ListForPartyAsync ─────────────────────────────────────────

    [Fact]
    public async Task ListForPartyAsync_Should_ScopeToPayerParty_And_PassFiltersAndPaging()
    {
        ListOrdersRequest? captured = null;
        _orderService
            .Setup(s => s.ListOrdersAsync(It.IsAny<ListOrdersRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ListOrdersRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new PagedResult<OrderListItem>(
                [BuildListItem()], TotalCount: 7, PageNumber: 2, PageSize: 5));

        var sut = CreateSut();

        var result = await sut.ListForPartyAsync(OwnerParty, status: "Submitted", orderType: "Transfer", pageNumber: 2, pageSize: 5);

        captured.Should().NotBeNull();
        captured!.PayerPartyId.Should().Be(OwnerParty);
        captured.Status.Should().Be("Submitted");
        captured.OrderType.Should().Be("Transfer");
        captured.PageNumber.Should().Be(2);
        captured.PageSize.Should().Be(5);
        captured.Search.Should().BeNull();

        // Paging metadata is preserved on the projected result.
        result.TotalCount.Should().Be(7);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(5);
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListForPartyAsync_Should_ProjectSummaryFields()
    {
        var createdAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var updatedAt = createdAt.AddHours(6);
        _orderService
            .Setup(s => s.ListOrdersAsync(It.IsAny<ListOrdersRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<OrderListItem>(
                [new OrderListItem(
                    OrderId: Guid.NewGuid(),
                    OrderType: "Transfer",
                    Status: "Processing",
                    PayerPartyId: OwnerParty,
                    PayerName: "Ada",
                    OriginCountry: "GB",
                    OriginCurrency: "GBP",
                    TotalAmountIn: 100m,
                    TotalAmountOut: 205000m,
                    DestinationCurrency: "NGN",
                    CreatedAt: createdAt,
                    UpdatedAt: updatedAt)],
                TotalCount: 1, PageNumber: 1, PageSize: 20));

        var sut = CreateSut();

        var summary = (await sut.ListForPartyAsync(OwnerParty, null, null, 1, 20)).Items.Single();

        summary.OrderType.Should().Be("Transfer");
        summary.Status.Should().Be("Processing");
        summary.OriginCurrency.Should().Be("GBP");
        summary.TotalAmountIn.Should().Be(100m);
        summary.DestinationCurrency.Should().Be("NGN");
        summary.TotalAmountOut.Should().Be(205000m);
        summary.CreatedAt.Should().Be(createdAt);
        summary.UpdatedAt.Should().Be(updatedAt);
    }

    // ── GetForPartyAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetForPartyAsync_Should_ReturnDetail_When_Owned()
    {
        var orderId = Guid.NewGuid();
        _orderService
            .Setup(s => s.GetOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrderResponse(orderId, OwnerParty));

        var sut = CreateSut();

        var detail = await sut.GetForPartyAsync(OwnerParty, orderId);

        detail.Should().NotBeNull();
        detail!.OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task GetForPartyAsync_Should_ReturnNull_When_OwnedByAnotherParty()
    {
        var orderId = Guid.NewGuid();
        _orderService
            .Setup(s => s.GetOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrderResponse(orderId, OtherParty));

        var sut = CreateSut();

        // Caller knows the id but is not the payer — must not leak the order.
        var detail = await sut.GetForPartyAsync(OwnerParty, orderId);

        detail.Should().BeNull();
    }

    [Fact]
    public async Task GetForPartyAsync_Should_ReturnNull_When_OrderNotFound()
    {
        var orderId = Guid.NewGuid();
        _orderService
            .Setup(s => s.GetOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException($"Order {orderId} not found."));

        var sut = CreateSut();

        var detail = await sut.GetForPartyAsync(OwnerParty, orderId);

        detail.Should().BeNull();
    }

    [Fact]
    public async Task GetForPartyAsync_Should_FlattenPrimaryItem_ByItemIndex()
    {
        var orderId = Guid.NewGuid();
        // Items supplied out of index order; the lowest ItemIndex is the primary line.
        var response = BuildOrderResponse(orderId, OwnerParty, items:
        [
            BuildItem(itemIndex: 2, receiverName: "Second Receiver", billerName: "Second Biller"),
            BuildItem(itemIndex: 0, receiverName: "First Receiver", billerName: "First Biller"),
        ]);
        _orderService
            .Setup(s => s.GetOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = CreateSut();

        var detail = await sut.GetForPartyAsync(OwnerParty, orderId);

        detail.Should().NotBeNull();
        detail!.ItemCount.Should().Be(2);
        detail.PrimaryReceiverName.Should().Be("First Receiver");
        detail.PrimaryBillerName.Should().Be("First Biller");
    }

    // ── CancelForPartyAsync ───────────────────────────────────────

    [Fact]
    public async Task CancelForPartyAsync_Should_Cancel_And_ReturnDetail_When_Owned()
    {
        var orderId = Guid.NewGuid();
        _orderService
            .Setup(s => s.GetOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrderResponse(orderId, OwnerParty, status: "Submitted"));
        _orderService
            .Setup(s => s.CancelOrderAsync(orderId, "changed my mind", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrderResponse(orderId, OwnerParty, status: "Cancelled"));

        var sut = CreateSut();

        var detail = await sut.CancelForPartyAsync(OwnerParty, orderId, "changed my mind");

        detail.Status.Should().Be("Cancelled");
        _orderService.Verify(s => s.CancelOrderAsync(orderId, "changed my mind", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelForPartyAsync_Should_Throw_And_NotCancel_When_OwnedByAnotherParty()
    {
        var orderId = Guid.NewGuid();
        _orderService
            .Setup(s => s.GetOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrderResponse(orderId, OtherParty, status: "Submitted"));

        var sut = CreateSut();

        // Security-critical: a non-owner must never reach the cancel mutation.
        var act = () => sut.CancelForPartyAsync(OwnerParty, orderId, reason: null);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _orderService.Verify(
            s => s.CancelOrderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Fixtures ──────────────────────────────────────────────────

    private static OrderListItem BuildListItem() => new(
        OrderId: Guid.NewGuid(),
        OrderType: "BillPayment",
        Status: "Submitted",
        PayerPartyId: OwnerParty,
        PayerName: "Ada",
        OriginCountry: "GB",
        OriginCurrency: "GBP",
        TotalAmountIn: 50m,
        TotalAmountOut: null,
        DestinationCurrency: null,
        CreatedAt: DateTime.UtcNow,
        UpdatedAt: null);

    private static BillPaymentOrderResponse BuildOrderResponse(
        Guid orderId,
        Guid payerPartyId,
        string status = "Submitted",
        IReadOnlyList<OrderItemResponse>? items = null) => new(
        OrderId: orderId,
        OrderType: "BillPayment",
        Status: status,
        PayerPartyId: payerPartyId,
        PayerName: "Ada",
        OriginCountry: "GB",
        OriginCurrency: "GBP",
        TotalAmountIn: 100m,
        TotalFeesAmount: 2m,
        TotalAmountOut: 205000m,
        DestinationCurrency: "NGN",
        PurposeCode: "FAMILY_SUPPORT",
        CreatedAt: DateTime.UtcNow,
        SubmittedAt: DateTime.UtcNow,
        Items: items ?? [BuildItem(0, "Receiver", "Biller")]);

    private static OrderItemResponse BuildItem(int itemIndex, string receiverName, string billerName) => new(
        OrderItemId: Guid.NewGuid(),
        ItemIndex: itemIndex,
        ItemType: "BillPayment",
        Status: "Submitted",
        BillerId: Guid.NewGuid(),
        BillerName: billerName,
        ServiceId: Guid.NewGuid(),
        ServiceCode: "SVC",
        ServiceName: "Service",
        ServiceFieldValues: new Dictionary<string, string>(),
        ReceiverPartyId: Guid.NewGuid(),
        ReceiverName: receiverName,
        RelationshipTypeCode: null,
        AmountIn: 100m,
        CurrencyIn: "GBP",
        AmountOut: 205000m,
        CurrencyOut: "NGN",
        FeesTotal: 2m,
        ExchangeRate: 2050m,
        PricingQuoteId: null,
        QuoteExpiresAt: null,
        IsQuoteExpired: false);
}
