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

// Add Qdrant vector store
var qdrant = builder
    .AddContainer("qdrant", "qdrant/qdrant", "v1.13.2")
    .WithHttpEndpoint(6333, 6333, name: "rest")
    .WithEndpoint("grpc", endpoint =>
    {
        endpoint.Port = 6334;
        endpoint.TargetPort = 6334;
    })
    .WithVolume("qdrant-data", "/qdrant/storage")
    .WithVolume("qdrant-snapshots", "/qdrant/snapshots")
    .WithEnvironment("QDRANT_API_KEY", "qdrant-dev-key")
    .WithEnvironment("QDRANT_SNAPSHOT_DIR", "/qdrant/snapshots");

// Spec 029 — conditional Keycloak local-dev container.
//
// Operators developing against Keycloak set AONIK_AUTH_PROVIDER=Keycloak (env
// var, case-insensitive) and `dotnet run --project src/Aonik.AppHost` brings up
// a single-node Keycloak alongside the rest of the platform. When the variable
// is unset (Auth0 / Azure AD developers), Keycloak is skipped — same one-command
// dev experience as before. The realm export at infra/keycloak/realm-export.json
// is mounted into the container, so login works against the pre-seeded `aonik`
// realm with no further setup.
//
// DEV-ONLY. The default admin credentials (admin / admin) and ephemeral storage
// make this unsuitable for any non-local use. Production Keycloak is operator-
// owned; see docs/operations/keycloak-setup.md.
var enableKeycloak = string.Equals(
    Environment.GetEnvironmentVariable("AONIK_AUTH_PROVIDER"),
    "Keycloak",
    StringComparison.OrdinalIgnoreCase);

if (enableKeycloak)
{
    var realmExportPath = Path.GetFullPath(
        Path.Combine(builder.AppHostDirectory, "..", "..", "infra", "keycloak", "realm-export.json"));

    builder
        .AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.0")
        .WithHttpEndpoint(8080, 8080, name: "http")
        .WithBindMount(realmExportPath, "/opt/keycloak/data/import/realm-export.json", isReadOnly: true)
        .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
        .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", "admin")
        .WithEnvironment("KC_HEALTH_ENABLED", "true")
        .WithEnvironment("KC_HTTP_PORT", "8080")
        .WithEnvironment("KC_HOSTNAME", "localhost")
        .WithEnvironment("KC_HOSTNAME_STRICT", "false")
        .WithEnvironment("KC_HTTP_ENABLED", "true")
        .WithArgs("start-dev", "--import-realm");
}

// Spec 029 — when Keycloak is the active provider, the realm authority
// and audience are the same dev-local values across api / worker / adminui.
// Pulling them into constants here keeps the per-resource wiring below
// readable and ensures the API + SPA agree on the same realm URL.
const string KeycloakAuthority = "http://localhost:8080/realms/aonik";
const string KeycloakAudience = "aonik-api";
const string KeycloakSpaClientId = "aonik-spa";

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
    .WaitFor(qdrant)
    .WithEnvironment("Qdrant__Endpoint", "http://qdrant:6333")
    .WithEnvironment("Qdrant__ApiKey", "qdrant-dev-key")
    .WithEnvironment("Qdrant__CollectionPrefix", "aonik-dev")
    .WithExternalHttpEndpoints();

if (enableKeycloak)
{
    // Auto-wire the API to the dev realm so the developer doesn't have to
    // touch user-secrets or appsettings just to try Keycloak. The realm
    // URL matches the container declared above; the audience matches the
    // `aonik-spa` client's audience mapper in infra/keycloak/realm-export.json.
    api.WithEnvironment("Auth__Provider", "Keycloak")
        .WithEnvironment("Auth__Keycloak__Authority", KeycloakAuthority)
        .WithEnvironment("Auth__Keycloak__Audience", KeycloakAudience);
}

// Add Worker project with LocalDB connection
var worker = builder.AddProject<Projects.Aonik_Worker>("worker")
    .WithEnvironment("ConnectionStrings__DefaultConnection", LocalDbConnectionString)
    .WithEnvironment("ConnectionStrings__AonikDb", LocalDbConnectionString)
    .WaitFor(qdrant)
    .WithEnvironment("Qdrant__Endpoint", "http://qdrant:6333")
    .WithEnvironment("Qdrant__ApiKey", "qdrant-dev-key")
    .WithEnvironment("Qdrant__CollectionPrefix", "aonik-dev");

// Add Admin UI (React/Vite frontend)
var adminUi = builder.AddViteApp("adminui", "../Aonik.AdminUi")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5173;
    })
    .WithEnvironment("VITE_API_BASE_URL", "/api")
    .WaitFor(api)
    .WithExternalHttpEndpoints();

if (enableKeycloak)
{
    // SPA OIDC client config — same realm URL, the public `aonik-spa` client
    // declared in the realm export. Vite reads these at dev-server boot, so a
    // single `dotnet run --project src/Aonik.AppHost` is enough to bring up a
    // working Keycloak login experience end-to-end (no .env.local needed).
    adminUi.WithEnvironment("VITE_AUTH_PROVIDER", "keycloak")
        .WithEnvironment("VITE_KEYCLOAK_AUTHORITY", KeycloakAuthority)
        .WithEnvironment("VITE_KEYCLOAK_CLIENT_ID", KeycloakSpaClientId);
}

// Add Payabo (React/Vite frontend)
var payabo = builder.AddViteApp("payabo", "../../apps/Payabo")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5174;
    })
    .WaitFor(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
