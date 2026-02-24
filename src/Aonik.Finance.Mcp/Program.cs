using Aonik.Agents;
using Aonik.Ai;
using Aonik.Finance;
using Aonik.Finance.Mcp.Hosting;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;

var builder = Host.CreateApplicationBuilder(args);

// ── Module Registration ──────────────────────────────────────────────
// Register the Finance, AI, and Agents modules so all domain services
// are available for MCP tool DI injection.
builder.Services.AddFinanceModule(builder.Configuration);
builder.Services.AddAiModule(builder.Configuration);
builder.Services.AddAgentsModule(builder.Configuration);

// ── Cross-Module Stubs ───────────────────────────────────────────────
// The Finance module services depend on cross-cutting abstractions
// normally provided by Infrastructure (HttpContext-based) and Platform.
// In the MCP server (a headless console process), we register lightweight
// stub/configurable implementations instead.
builder.Services.AddSingleton<IClock, McpSystemClock>();
builder.Services.AddSingleton<ITenantProvider>(new McpTenantProvider(
    builder.Configuration.GetValue<Guid?>("McpTenantId")
        ?? Guid.Parse("00000000-0000-0000-0000-000000000001")));
builder.Services.AddSingleton<ICurrentUserProvider>(new McpCurrentUserProvider(
    builder.Configuration.GetValue<Guid?>("McpUserId")
        ?? Guid.Parse("00000000-0000-0000-0000-000000000001")));
builder.Services.AddSingleton<IPermissionService, McpPermissionService>();
builder.Services.AddSingleton<IAuditLogWriter, McpAuditLogWriter>();
builder.Services.AddSingleton<IPartyService, McpPartyService>();
builder.Services.AddSingleton<IComplianceService, McpComplianceService>();
builder.Services.AddSingleton<ITenantCurrencyProvider, McpTenantCurrencyProvider>();

// ── MCP Server ───────────────────────────────────────────────────────
// Uses stdio transport for communication with MCP clients (agents, IDEs, etc.).
// Tools are discovered automatically from [McpServerToolType] classes in this assembly.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
