using Aonik.Commerce.Services.Checkout;
using Aonik.Infrastructure.Multitenancy;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;
using Aonik.Worker.Jobs;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OptionsFactory = Microsoft.Extensions.Options.Options;
using Moq;
using Quartz;

namespace Aonik.Application.Tests.Commerce;

public sealed class DeliveryReservationSweepJobTests
{
    [Fact]
    public async Task Execute_Should_PagePastFailuresAndDisabledTenants_UsingAFreshTenantScopeForEveryAttempt()
    {
        var enabledTenant = Guid.NewGuid();
        var disabledTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var failedCart = Guid.NewGuid();
        var skippedCart = Guid.NewGuid();
        var secondCart = Guid.NewGuid();
        var thirdCart = Guid.NewGuid();
        IReadOnlyList<(Guid ReservationId, Guid TenantId, Guid CartId)> first =
            [(Guid.NewGuid(), enabledTenant, failedCart), (Guid.NewGuid(), disabledTenant, skippedCart)];
        IReadOnlyList<(Guid ReservationId, Guid TenantId, Guid CartId)> second =
            [(Guid.NewGuid(), enabledTenant, secondCart), (Guid.NewGuid(), otherTenant, thirdCart)];
        var cursors = new List<Guid?>();
        var discovery = new Mock<ICheckoutService>(MockBehavior.Strict);
        discovery.Setup(s => s.FindDueDeliveryReservationsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Returns((Guid? after, CancellationToken _) =>
            {
                cursors.Add(after);
                IReadOnlyList<(Guid ReservationId, Guid TenantId, Guid CartId)> page = after is null ? first
                    : after == first[^1].ReservationId ? second : [];
                return Task.FromResult(page);
            });
        var modules = new Mock<IModuleEnablementReader>(MockBehavior.Strict);
        modules.Setup(m => m.FilterEnabledTenantsAsync(It.IsAny<IEnumerable<Guid>>(), ModuleIds.Commerce, It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<Guid> ids, string _, CancellationToken _) =>
                Task.FromResult<IReadOnlyList<Guid>>(ids.Where(id => id != disabledTenant).ToList()));
        var attempts = new List<(Guid CartId, ITenantContext Tenant)>();
        var services = new ServiceCollection();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ICheckoutService>(provider =>
        {
            var tenant = provider.GetRequiredService<ITenantContext>();
            var checkout = new Mock<ICheckoutService>(MockBehavior.Strict);
            checkout.Setup(s => s.ReconcileDeliveryReservationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns((Guid cartId, CancellationToken _) =>
                {
                    attempts.Add((cartId, tenant));
                    return cartId == failedCart ? Task.FromException(new TimeoutException()) : Task.CompletedTask;
                });
            return checkout.Object;
        });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var job = new DeliveryReservationSweepJob(discovery.Object, provider.GetRequiredService<IServiceScopeFactory>(),
            OptionsFactory.Create(new ScheduledJobOptions()), NullLogger<DeliveryReservationSweepJob>.Instance, modules.Object);
        var context = JobContext();

        await job.Execute(context);

        cursors.Should().Equal(null, first[^1].ReservationId, second[^1].ReservationId);
        attempts.Select(a => a.CartId).Should().Equal(failedCart, secondCart, thirdCart);
        attempts.Select(a => a.Tenant.TenantId).Should().Equal(enabledTenant, enabledTenant, otherTenant);
        attempts.Select(a => a.Tenant).Should().OnlyHaveUniqueItems();
        attempts.Should().OnlyContain(a => a.Tenant.ResolutionSource == "delivery-reservation-sweep");
        context.Result.Should().Be("Checked 2 delivery reservation(s); 1 require another reconciliation.");
        modules.Verify(m => m.FilterEnabledTenantsAsync(It.IsAny<IEnumerable<Guid>>(), ModuleIds.Commerce,
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Execute_Should_NotDiscoverOrResolveTenantServices_WhenDisabled()
    {
        var options = new ScheduledJobOptions();
        options.DeliveryReservationSweep.Enabled = false;
        var discovery = new Mock<ICheckoutService>(MockBehavior.Strict);
        var scopes = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        var modules = new Mock<IModuleEnablementReader>(MockBehavior.Strict);
        var job = new DeliveryReservationSweepJob(discovery.Object, scopes.Object, OptionsFactory.Create(options),
            NullLogger<DeliveryReservationSweepJob>.Instance, modules.Object);
        var context = JobContext();

        await job.Execute(context);

        context.Result.Should().Be("Delivery reservation sweep disabled.");
        discovery.VerifyNoOtherCalls();
        scopes.VerifyNoOtherCalls();
        modules.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Execute_Should_PropagateCancellation_BeforeDiscoveringReservations()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var discovery = new Mock<ICheckoutService>(MockBehavior.Strict);
        var job = new DeliveryReservationSweepJob(discovery.Object, new Mock<IServiceScopeFactory>(MockBehavior.Strict).Object,
            OptionsFactory.Create(new ScheduledJobOptions()), NullLogger<DeliveryReservationSweepJob>.Instance,
            new Mock<IModuleEnablementReader>(MockBehavior.Strict).Object);

        await job.Invoking(j => j.Execute(JobContext(cancellation.Token))).Should().ThrowAsync<OperationCanceledException>();

        discovery.VerifyNoOtherCalls();
    }

    private static IJobExecutionContext JobContext(CancellationToken cancellationToken = default)
    {
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(c => c.CancellationToken).Returns(cancellationToken);
        context.SetupProperty(c => c.Result);
        return context.Object;
    }
}
