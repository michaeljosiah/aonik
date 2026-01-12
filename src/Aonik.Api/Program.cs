using Aonik.Api.Configuration;
using Aonik.Api.Middleware;
using Aonik.Application;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Infrastructure;
using Aonik.Infrastructure.Persistence;
using Aonik.Infrastructure.Persistence.Seed;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add Application and Infrastructure layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

// Add AONIK Authentication & Authorization
builder.Services.AddAonikAuthenticationAndAuthorization(builder.Configuration);

// Add FastEndpoints
builder.Services.AddFastEndpoints();

// Add Swagger with OAuth2 authentication
builder.Services.AddAonikSwagger(builder.Configuration);

var app = builder.Build();

// Auto-migrate database (optional)
if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Seed permissions in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<IAonikDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<IdentitySeedService>>();
    var seedService = new IdentitySeedService(dbContext, logger);
    await seedService.SeedAsync();
}


// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseAonikSwagger(builder.Configuration);
}

// Map default Aspire endpoints (health, metrics)
app.MapDefaultEndpoints();

app.UseHttpsRedirection();

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
