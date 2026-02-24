using Aonik.Agents;
using Aonik.Ai;
using Aonik.Finance;
using Aonik.Platform;
using Aonik.Platform.Mcp.Hosting;
using Aonik.Platform.Contracts.Services.Storage;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;

var builder = Host.CreateApplicationBuilder(args);

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
builder.Services.AddSingleton<IDocumentFileStore, McpDocumentFileStore>();

// ── MCP Server ───────────────────────────────────────────────────────
// Uses stdio transport for communication with MCP clients (agents, IDEs, etc.).
// Tools are discovered automatically from [McpServerToolType] classes in this assembly.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
