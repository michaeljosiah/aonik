using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Persistence;
using Aonik.Publishing.Services;
using Aonik.SharedKernel.Modules;
using Aonik.SharedKernel.Events;

namespace Aonik.Publishing;

public sealed class PublishingModule : IModule
{
    public static string Name => "Publishing";
    public static string Id => ModuleIds.Publishing;

    public static IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PublishingOptions>(configuration.GetSection(PublishingOptions.SectionName));
        services.AddDbContext<PublishingDbContext>((_, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
                options.UseInMemoryDatabase(configuration.GetValue<string>("InMemoryDatabaseName") ?? "PublishingDb");
            else
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")
                    ?? configuration.GetConnectionString("AonikDb")
                    ?? "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;",
                    sql => sql.EnableRetryOnFailure());
        });
        services.AddScoped<IPublishingDataContext>(sp => sp.GetRequiredService<PublishingDbContext>());
        services.AddScoped<CreatorSubmissionService>();
        services.AddScoped<ICreatorSubmissionService>(sp => sp.GetRequiredService<CreatorSubmissionService>());
        services.AddScoped<IPublicationIntakeService, PublicationIntakeService>();
        services.AddScoped<IPublicationUploadMaintenance, PublicationUploadMaintenance>();
        services.AddScoped<EditorialReviewAuthorization>();
        services.AddScoped<IEditorialReviewService, EditorialReviewService>();
        services.AddScoped<ISubmissionRevisionService, SubmissionRevisionService>();
        services.AddScoped<PublicationAuthority>();
        services.AddScoped<IPublicationService, PublicationService>();
        services.AddScoped<PublicPublicationReader>();
        services.AddEventHandlersFromAssembly(typeof(PublishingModule).Assembly);
        return services;
    }
}

public static class PublishingModuleExtensions
{
    public static IServiceCollection AddPublishingModule(this IServiceCollection services, IConfiguration configuration)
        => PublishingModule.ConfigureServices(services, configuration);
}
