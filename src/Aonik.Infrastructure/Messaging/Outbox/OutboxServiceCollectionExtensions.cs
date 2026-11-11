using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Aonik.Publishing.Services;

namespace Aonik.Infrastructure.Messaging.Outbox;

/// <summary>
/// Registration entry point for the transactional outbox processor.
/// </summary>
public static class OutboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers the outbox dispatcher, processor, and its hosted drainer. Call this
    /// ONLY in the Worker host so a single process dispatches events. The SharedKernel
    /// event assembly is always scanned for <c>IIntegrationEvent</c> types;
    /// <paramref name="eventAssemblies"/> adds any modules that define their own.
    /// Handlers are resolved from DI (registered via <c>AddEventBus</c>), so this does
    /// not register them.
    /// </summary>
    public static IServiceCollection AddOutboxProcessing(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] eventAssemblies)
    {
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddOptions<OutboxOptions>().Configure<IOptions<PublishingOptions>>((outbox, publishing) =>
        {
            if (publishing.Value.Verifier.CapacityProfile != PackageCapacity.LargeProfile) return;
            // Claimed batches run sequentially. Do not lease other messages behind a large verification.
            outbox.BatchSize = 1;
            outbox.ClaimLeaseSeconds = Math.Max(outbox.ClaimLeaseSeconds, PackageCapacity.Large.LeaseSeconds + 60);
        });

        services.AddSingleton<IIntegrationEventTypeRegistry>(
            _ => new IntegrationEventTypeRegistry(eventAssemblies));

        services.AddScoped<IIntegrationEventDispatcher, IntegrationEventDispatcher>();
        services.AddSingleton<OutboxProcessor>();
        services.AddHostedService<OutboxBackgroundService>();

        return services;
    }
}
