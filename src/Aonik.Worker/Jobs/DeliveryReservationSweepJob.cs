using Aonik.Commerce.Services.Checkout;
using Aonik.Platform.Entities.Operations;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

namespace Aonik.Worker.Jobs;

[DisallowConcurrentExecution]
internal sealed class DeliveryReservationSweepJob(
    ICheckoutService discovery,
    IServiceScopeFactory scopeFactory,
    IOptions<ScheduledJobOptions> options,
    ILogger<DeliveryReservationSweepJob> logger,
    IModuleEnablementReader moduleReader) : IJob
{
    public static readonly JobKey Key = new("DeliveryReservationSweepJob", ScheduledJobGroups.ScheduledJobs);

    public async Task Execute(IJobExecutionContext context)
    {
        if (!options.Value.DeliveryReservationSweep.Enabled)
        {
            context.Result = "Delivery reservation sweep disabled.";
            return;
        }

        var checkedCount = 0;
        var failedCount = 0;
        Guid? after = null;
        while (true)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var page = await discovery.FindDueDeliveryReservationsAsync(after, context.CancellationToken);
            if (page.Count == 0) break;
            var gate = await ModuleGatedTenants.FilterAsync(moduleReader,
                page.Select(r => r.TenantId).Distinct().ToList(), ModuleIds.Commerce,
                "Delivery reservation sweep", logger, context.CancellationToken);
            foreach (var item in page.Where(r => gate.Enabled.Contains(r.TenantId)))
            {
                // Separate scopes prevent a failed provider attempt or tenant switch from
                // leaving tracked changes in the next reservation's unit of work.
                await using var scope = scopeFactory.CreateAsyncScope();
                var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                tenant.TenantId = item.TenantId;
                tenant.ResolutionSource = "delivery-reservation-sweep";
                try
                {
                    await scope.ServiceProvider.GetRequiredService<ICheckoutService>()
                        .ReconcileDeliveryReservationAsync(item.CartId, context.CancellationToken);
                    checkedCount++;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failedCount++;
                    logger.LogWarning("Delivery reconciliation retained reservation {ReservationId} for tenant {TenantId}: {ErrorType}",
                        item.ReservationId, item.TenantId, exception.GetType().Name);
                }
            }
            // Seek past unresolved attempts too, so an unknown provider result cannot starve
            // later holds. Each query is bounded and each row is attempted once per sweep.
            after = page[^1].ReservationId;
        }
        context.Result = $"Checked {checkedCount} delivery reservation(s); {failedCount} require another reconciliation.";
    }
}
