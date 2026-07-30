using Aonik.Agents;
using Aonik.Ai;
using Aonik.Finance;
using Aonik.Platform;
using Aonik.Platform.Mcp.Hosting;
using Aonik.Platform.Contracts.Services.Storage;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.SharedKernel.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;

var builder = Host.CreateApplicationBuilder(args);

// ── Fail-closed environment guard (backend review finding C4) ────────
// This server authenticates every call as a fixed PlatformAdmin identity (see
// McpCurrentUserContext) with a fixed tenant/user — a blanket-trust stand-in that
// is safe only in Development. Refuse to start anywhere else so this ambient-admin
// context can never be exposed outside Development.
DevelopmentOnlyHostGuard.EnsureDevelopmentOnly(
    builder.Environment.EnvironmentName,
    "The Platform MCP server",
    "It authenticates every call as a fixed PlatformAdmin identity with a fixed tenant/user.");

// ── Configuration ────────────────────────────────────────────────────
var tenantId = builder.Configuration.GetValue<Guid?>("McpTenantId")
    ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
var userId = builder.Configuration.GetValue<Guid?>("McpUserId")
    ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

// ── Module Registration ──────────────────────────────────────────────
// Register all modules so domain services are available for MCP tool DI injection.
// Platform depends on Finance (for ICurrencyMetadataProvider registered by Finance
// — but we provide our own stub), AI, and Agents.
builder.Services.AddPlatformModule(builder.Configuration);
// Ordering is the only registration of the SharedKernel IOrderService, which Platform's
// customer admin service needs for the Spec 080 registry read-model. This host runs forced
// Development, where the container validates every registration at Build(), so omitting it
// fails the host outright rather than at first use.
builder.Services.AddOrderingModule(builder.Configuration);
builder.Services.AddFinanceModule(builder.Configuration);
builder.Services.AddAiModule(builder.Configuration);
builder.Services.AddAgentsModule(builder.Configuration);

// ── Cross-Module Stubs ───────────────────────────────────────────────
// Replace HttpContext-based Infrastructure implementations with lightweight
// stubs suitable for a headless console process.
builder.Services.AddSingleton<IClock, McpSystemClock>();
builder.Services.AddSingleton<ITenantProvider>(new McpTenantProvider(tenantId));
builder.Services.AddSingleton<ITenantContext>(new McpTenantContext(tenantId));
builder.Services.AddSingleton<ICurrentUserProvider>(new McpCurrentUserProvider(userId));
builder.Services.AddSingleton<ICurrentUserContext>(new McpCurrentUserContext(userId, tenantId));
builder.Services.AddSingleton<ICorrelationContext, McpCorrelationContext>();
builder.Services.AddSingleton<ICurrencyMetadataProvider, McpCurrencyMetadataProvider>();
builder.Services.AddSingleton<IProfilePhotoStore, McpProfilePhotoStore>();
builder.Services.AddSingleton<Aonik.SharedKernel.Abstractions.Documents.IDocumentFileStore, McpDocumentFileStore>();

// ── MCP Server ───────────────────────────────────────────────────────
// Uses stdio transport for communication with MCP clients (agents, IDEs, etc.).
// Tools are discovered automatically from [McpServerToolType] classes in this assembly.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
