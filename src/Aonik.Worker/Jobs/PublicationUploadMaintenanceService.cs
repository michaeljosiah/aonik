using Aonik.Publishing.Services;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Worker.Jobs;

/// <summary>Retires already committed, abandoned upload reservations. Never removes submitted editions.</summary>
internal sealed class PublicationUploadMaintenanceService(IServiceScopeFactory scopes,
    ILogger<PublicationUploadMaintenanceService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var discovery = scopes.CreateAsyncScope();
                var tenants = await discovery.ServiceProvider.GetRequiredService<IPublicationUploadMaintenance>().FindTenantsAsync(stoppingToken);
                foreach (var tenant in tenants)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var context = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                    context.TenantId = tenant; context.ResolutionSource = "publication-upload-maintenance";
                    await scope.ServiceProvider.GetRequiredService<IPublicationUploadMaintenance>().SweepAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Publication upload maintenance failed; retained objects remain owned."); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}
