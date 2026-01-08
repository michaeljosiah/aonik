using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Models.Payments;
using Aonik.Application.Services.Payments;
using Aonik.Domain.Payments;
using Aonik.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Payments;

public class PaymentServiceTests
{
    private class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;

        public Guid GetCurrentTenantId() => _tenantId;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }
    }

    private static AonikDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new AonikDbContext(options);
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_ShouldCreatePaymentIntent()
    {
        // Arrange
        using var context = CreateDbContext();
        var tenantProvider = new TestTenantProvider(Guid.NewGuid());
        var service = new PaymentService(context, tenantProvider);
        var request = new CreatePaymentIntentRequest(100.00m, "USD", "ORDER-001");

        // Act
        var result = await service.CreatePaymentIntentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Amount.Should().Be(100.00m);
        result.Currency.Should().Be("USD");
        result.Reference.Should().Be("ORDER-001");
        result.Status.Should().Be(PaymentStatus.Pending);
        result.CreatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        var savedPayment = await context.PaymentIntents.FirstOrDefaultAsync(p => p.Id == result.Id);
        savedPayment.Should().NotBeNull();
        savedPayment!.Amount.Should().Be(100.00m);
    }

    [Fact]
    public async Task GetPaymentIntentAsync_ShouldReturnPaymentIntent_WhenExists()
    {
        // Arrange
        using var context = CreateDbContext();
        var tenantProvider = new TestTenantProvider(Guid.NewGuid());
        var service = new PaymentService(context, tenantProvider);
        var createRequest = new CreatePaymentIntentRequest(250.00m, "EUR", "ORDER-002");
        var created = await service.CreatePaymentIntentAsync(createRequest);

        // Act
        var result = await service.GetPaymentIntentAsync(created.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.Amount.Should().Be(250.00m);
        result.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task GetPaymentIntentAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        using var context = CreateDbContext();
        var tenantProvider = new TestTenantProvider(Guid.NewGuid());
        var service = new PaymentService(context, tenantProvider);

        // Act
        var result = await service.GetPaymentIntentAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CapturePaymentAsync_ShouldCapturePayment_WhenAuthorized()
    {
        // Arrange
        using var context = CreateDbContext();
        var tenantProvider = new TestTenantProvider(Guid.NewGuid());
        var service = new PaymentService(context, tenantProvider);
        var createRequest = new CreatePaymentIntentRequest(100.00m, "USD", "ORDER-003");
        var created = await service.CreatePaymentIntentAsync(createRequest);

        // Authorize the payment first (set status directly since entities are anemic)
        var payment = await context.PaymentIntents.FirstAsync(p => p.Id == created.Id);
        payment.Status = "Authorized";
        await context.SaveChangesAsync();

        // Act
        var result = await service.CapturePaymentAsync(created.Id);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(PaymentStatus.Captured);
    }

    [Fact]
    public async Task CapturePaymentAsync_ShouldThrow_WhenPaymentNotFound()
    {
        // Arrange
        using var context = CreateDbContext();
        var tenantProvider = new TestTenantProvider(Guid.NewGuid());
        var service = new PaymentService(context, tenantProvider);

        // Act
        var act = async () => await service.CapturePaymentAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Payment intent with ID * not found");
    }

    [Fact]
    public async Task CancelPaymentAsync_ShouldCancelPayment_WhenPending()
    {
        // Arrange
        using var context = CreateDbContext();
        var tenantProvider = new TestTenantProvider(Guid.NewGuid());
        var service = new PaymentService(context, tenantProvider);
        var createRequest = new CreatePaymentIntentRequest(100.00m, "USD", "ORDER-004");
        var created = await service.CreatePaymentIntentAsync(createRequest);

        // Act
        var result = await service.CancelPaymentAsync(created.Id);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(PaymentStatus.Cancelled);
    }

    [Fact]
    public async Task CancelPaymentAsync_ShouldThrow_WhenPaymentNotFound()
    {
        // Arrange
        using var context = CreateDbContext();
        var tenantProvider = new TestTenantProvider(Guid.NewGuid());
        var service = new PaymentService(context, tenantProvider);

        // Act
        var act = async () => await service.CancelPaymentAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Payment intent with ID * not found");
    }
}
