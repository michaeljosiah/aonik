using Aonik.Application.Abstractions.Ai;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Infrastructure.Ai.Prompting;
using Aonik.Infrastructure.Ai.Providers;
using Aonik.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;";

        services.AddDbContext<AonikDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IAonikDbContext>(sp => sp.GetRequiredService<AonikDbContext>());

        // AI
        services.AddSingleton<IPromptStore>(sp =>
        {
            var promptPath = configuration["AI:PromptTemplatesPath"];
            return new FileBasedPromptStore(promptPath);
        });

        services.AddScoped<IModelProvider, StubModelProvider>();

        return services;
    }
}
