using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.SharedKernel.Modules;

/// <summary>
/// Represents a self-contained module in the modular monolith.
/// Each module registers its own services, DbContext, and endpoints.
/// </summary>
public interface IModule
{
    /// <summary>
    /// Unique name for this module (e.g., "Platform", "Finance", "Ai").
    /// </summary>
    static abstract string Name { get; }

    /// <summary>
    /// Registers the module's services into the DI container.
    /// Called from the composition root (Aonik.Api Program.cs).
    /// </summary>
    static abstract IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration);
}
