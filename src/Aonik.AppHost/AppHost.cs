var builder = DistributedApplication.CreateBuilder(args);

// Add SQL Server with a database
var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent);

var sqlServer = sql.AddDatabase("AonikDb");

// Add API project with SQL Server reference
var api = builder.AddProject<Projects.Aonik_Api>("api")
     .WithEndpoint("https", endpoint =>
     {
         endpoint.Port = 5001;
     })
     .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
     .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
     .WithEnvironment("Database__AutoMigrate", "true")
     .WithEnvironment("Database__SeedData", "true")
     .WithReference(sqlServer)
     .WaitFor(sqlServer)
     .WithExternalHttpEndpoints();

// Add Worker project with SQL Server reference
var worker = builder.AddProject<Projects.Aonik_Worker>("worker")
    .WithReference(sqlServer)
    .WaitFor(sqlServer);

// Add Admin UI (React/Vite frontend)
var adminUi = builder.AddViteApp("adminui", "../Aonik.AdminUi")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5173;
    })
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
