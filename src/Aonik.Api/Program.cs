using Aonik.Api.Configuration;
using Aonik.Api.Middleware;
using Aonik.Application;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Infrastructure;
using FastEndpoints;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Aonik.Infrastructure.Persistence;
using Aonik.Infrastructure.Persistence.Seed;
using Aonik.Platform;
using Aonik.Finance;


var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add Application and Infrastructure layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

// Add domain modules
builder.Services.AddPlatformModule(builder.Configuration);
builder.Services.AddFinanceModule(builder.Configuration);

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add AONIK Authentication & Authorization
builder.Services.AddAonikAuthenticationAndAuthorization(builder.Configuration);

// Add FastEndpoints
builder.Services.AddFastEndpoints();

// Use string enums in JSON
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Add Swagger with OAuth2 authentication
builder.Services.AddAonikSwagger(builder.Configuration);

var app = builder.Build();

// Auto-migrate and seed database in Development or when enabled via config
var autoMigrateEnabled = app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("Database:AutoMigrate");
var seedDataEnabled = app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("Database:SeedData");

if (autoMigrateEnabled || seedDataEnabled)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();

    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        if (autoMigrateEnabled)
        {
            startupLogger.LogInformation("Running database migrations...");
            await dbContext.Database.MigrateAsync();
            startupLogger.LogInformation("Database migrations completed successfully.");
        }

        if (seedDataEnabled)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<IdentitySeedService>>();
            var seedService = new IdentitySeedService((IAonikDbContext)dbContext, logger);
            await seedService.SeedAsync();

            var catalogLogger = scope.ServiceProvider.GetRequiredService<ILogger<CatalogSeedService>>();
            var catalogSeedService = new CatalogSeedService((IAonikDbContext)dbContext, catalogLogger);
            await catalogSeedService.SeedAsync();

            var settingsLogger = scope.ServiceProvider.GetRequiredService<ILogger<SettingsSeedService>>();
            var settingsSeedService = new SettingsSeedService((IAonikDbContext)dbContext, settingsLogger);
            await settingsSeedService.SeedAsync();
        }
    }
    catch (Exception ex)
    {
        startupLogger.LogWarning(ex, "Skipping database initialization due to connectivity issues.");
    }
}



// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseAonikSwagger(builder.Configuration);
}

// Map default Aspire endpoints (health, metrics)
app.MapDefaultEndpoints();

// Use HTTPS redirection
app.UseHttpsRedirection();

// Use CORS for development
if (app.Environment.IsDevelopment())
{
    app.UseCors("Development");
}

// Serve static files for local blob storage (profile photos, etc.)
var blobStorageProvider = builder.Configuration["BlobStorage:Provider"];
if (string.Equals(blobStorageProvider, "Local", StringComparison.OrdinalIgnoreCase))
{
    var localBasePath = builder.Configuration["BlobStorage:LocalBasePath"] ?? "App_Data";
    var profilePhotosPath = builder.Configuration["BlobStorage:ProfilePhotos:Path"] ?? "profiles";
    var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), localBasePath, profilePhotosPath);
    
    // Create directory if it doesn't exist
    Directory.CreateDirectory(physicalPath);
    
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(physicalPath),
        RequestPath = "/storage/profiles",
        OnPrepareResponse = ctx =>
        {
            // Cache images for 1 hour
            ctx.Context.Response.Headers.CacheControl = "public, max-age=3600";
        }
    });
}

// CRITICAL: Middleware order matters!
// 1. Authentication (validates JWT, runs OnTokenValidated)
app.UseAuthentication();

// 2. Tenant context resolution
app.UseTenantContext();

// 3. Authorization (checks policies/permissions)
app.UseAuthorization();

// 4. Tenant validation (validates tenant status only)
app.UseTenantValidation();

// 5. FastEndpoints
app.UseFastEndpoints();

app.Run();

// Make the Program class accessible for testing
public partial class Program { }
