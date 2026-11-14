using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Infrastructure.Integrations.Gmail;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Infrastructure.Integrations;

internal static class ConnectorInfrastructureRegistration
{
    public static void AddConnectorInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GmailConnectorOptions>(configuration.GetSection(GmailConnectorOptions.Section));
        services.Configure<GmailMailboxOptions>(configuration.GetSection(GmailMailboxOptions.Section));
        foreach (string name in new[] { "Gmail", "GmailOAuth" })
            services.AddHttpClient(name, client => client.Timeout = TimeSpan.FromSeconds(15))
                .RemoveAllLoggers()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        services.AddScoped<IConnectorOAuthProvider, GmailOAuthProvider>();
        services.AddScoped<IConnectorActionProvider, GmailActionProvider>();
        services.AddScoped<GmailMailboxSyncProvider>();
        services.AddScoped<IConnectorMailboxSyncProvider>(provider => provider.GetRequiredService<GmailMailboxSyncProvider>());
        services.AddScoped<IConnectorMailboxPushVerifier, GmailMailboxPushVerifier>();
        services.AddScoped<IConnectorContentStore, ConnectorContentStore>();
    }
}
