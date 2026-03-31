var hasDashboardFrontend = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));
var hasDashboardOtlpEndpoint =
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL")) ||
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL"));

// Aspire 13.2 validates dashboard endpoints eagerly. When the app host is started
// without its launch profile, those variables are absent, so disable the dashboard
// instead of crashing the host.
var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
{
    Args = args,
    DisableDashboard = !hasDashboardFrontend || !hasDashboardOtlpEndpoint,
});

const string LocalDbConnectionString = @"Server=(localdb)\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;";

// Add API project with LocalDB connection
var api = builder.AddProject<Projects.Aonik_Api>("api")
    .WithEndpoint("https", endpoint =>
    {
        endpoint.Port = 5001;
    })
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
    .WithEnvironment("UseInMemoryDatabase", "false")
    .WithEnvironment("ConnectionStrings__DefaultConnection", LocalDbConnectionString)
    .WithEnvironment("ConnectionStrings__AonikDb", LocalDbConnectionString)
    .WithEnvironment("Database__AutoMigrate", "true")
    .WithEnvironment("Database__SeedData", "true")
    .WithExternalHttpEndpoints();

// Add Worker project with LocalDB connection
var worker = builder.AddProject<Projects.Aonik_Worker>("worker")
    .WithEnvironment("ConnectionStrings__DefaultConnection", LocalDbConnectionString)
    .WithEnvironment("ConnectionStrings__AonikDb", LocalDbConnectionString);

// Add Admin UI (React/Vite frontend)
var adminUi = builder.AddViteApp("adminui", "../Aonik.AdminUi")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5173;
    })
    .WithEnvironment("VITE_API_BASE_URL", "/api")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

// Add Payabo (React/Vite frontend)
var payabo = builder.AddViteApp("payabo", "../../apps/Payabo")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5174;
    })
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
