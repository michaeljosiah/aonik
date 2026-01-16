var builder = DistributedApplication.CreateBuilder(args);

// Add SQL Server with a database
var sqlServer = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase("aonikdb");

// Add API project with SQL Server reference
var api = builder.AddProject<Projects.Aonik_Api>("api")
    .WithReference(sqlServer)
    .WithExternalHttpEndpoints();

// Add Worker project with SQL Server reference
var worker = builder.AddProject<Projects.Aonik_Worker>("worker")
    .WithReference(sqlServer);

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
