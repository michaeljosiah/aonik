using Aonik.Groups.Persistence;
using Aonik.Groups.Services;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Modules;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Groups;

/// <summary>
/// Composition root for the groups and sharing module (Spec 086 / ADR-015).
/// </summary>
public sealed class GroupsModule : IModule
{
    public static string Name => "Groups";
    public static string Id => ModuleIds.Groups;

    /// <summary>
    /// Registers the group service. Deliberately does <b>not</b> register an
    /// <see cref="IGroupDataContext"/>: which context the group entities are written through is the
    /// composing application's decision, because it determines what shares a transaction with the
    /// membership write. PersonalFinance registers its own context for exactly that reason.
    /// </summary>
    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<GroupService>();
        services.AddScoped<IGroupService>(sp => sp.GetRequiredService<GroupService>());
        services.AddScoped<IGroupReader>(sp => sp.GetRequiredService<GroupService>());

        services.AddScoped<ShareGrantService>();
        services.AddScoped<IShareGrantService>(sp => sp.GetRequiredService<ShareGrantService>());
        services.AddScoped<IShareGrantReader>(sp => sp.GetRequiredService<ShareGrantService>());

        return services;
    }
}

public static class GroupsModuleExtensions
{
    public static IServiceCollection AddGroupsModule(
        this IServiceCollection services,
        IConfiguration configuration)
        => GroupsModule.ConfigureServices(services, configuration);
}
