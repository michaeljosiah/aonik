var builder = DistributedApplication.CreateBuilder(args);

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
