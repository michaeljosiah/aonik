using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers non-Platform, non-Finance Application services.
    /// Platform services are registered by <see cref="Aonik.Platform.PlatformModule"/>.
    /// Finance services are registered by <see cref="Aonik.Finance.FinanceModule"/>.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
