using Aonik.Agents;
using Aonik.Agents.Endpoints;
using Aonik.Ai;
using Aonik.Infrastructure.ExternalServices.Plaid;
using Aonik.Api.Configuration;
using Aonik.Api.Middleware;
using Aonik.Application;
using Aonik.Commerce;
using Aonik.Documents;
using Aonik.Finance;
using Aonik.Infrastructure;
using Aonik.Infrastructure.VectorStore;
using Aonik.Ordering;
using Aonik.PersonalFinance;
using Aonik.Platform;
using Aonik.Platform.Endpoints.Admin.Notifications;
using Aonik.Voice;
using Aonik.Voice.Endpoints;
using FastEndpoints;
using Microsoft.AspNetCore.HttpOverrides;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ── Service registration ──────────────────────────────────────────────

builder.AddServiceDefaults();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

builder.Services.AddPlatformModule(builder.Configuration);
builder.Services.AddOrderingModule(builder.Configuration);
builder.Services.AddFinanceModule(builder.Configuration);
builder.Services.AddCommerceModule(builder.Configuration);
builder.Services.AddPersonalFinanceModule(builder.Configuration);
builder.Services.AddAiModule(builder.Configuration);
builder.Services.AddAgentsModule(builder.Configuration);
builder.Services.AddAonikVoiceModule(builder.Configuration);
builder.Services.AddDocumentsModule(builder.Configuration);

builder.Services.AddAonikCors(builder.Configuration);
builder.Services.AddAonikAuthenticationAndAuthorization(builder.Configuration);

// FastEndpoints — explicitly enumerate the module assemblies so endpoints
// AND validators (Validator<TRequest>) defined in each module are
// discovered at startup. Relying on AppDomain probing alone is fragile
// because module DLLs are not loaded until their first reference.
builder.Services.AddFastEndpoints(o =>
{
    o.Assemblies =
    [
        typeof(PlatformModule).Assembly,
        typeof(FinanceModule).Assembly,
        typeof(PersonalFinanceModule).Assembly,
        typeof(AiModule).Assembly,
        typeof(AgentsModule).Assembly,
        typeof(AonikVoiceModule).Assembly,
        typeof(DocumentsModule).Assembly,
        typeof(CommerceModule).Assembly,
    ];
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddAonikSwagger(builder.Configuration);

// ── Build + database startup ──────────────────────────────────────────

var app = builder.Build();

app.Services.LogResolvedDatabaseConnection();
await app.InitializeAonikDatabaseAsync();

// Resolve runtime-mode toggles ONCE up-front so the per-request DI
// factories that depend on them (e.g. IUserMemoryService) stay
// allocation-free and never block a thread-pool thread on a settings
// lookup.
await app.InitializeUserMemoryBackendAsync();

// ── HTTP request pipeline ─────────────────────────────────────────────

app.MapDefaultEndpoints();

// Forward headers so ASP.NET Core recognises the original HTTPS scheme
// behind ACA's TLS-terminating ingress.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Exception handler must wrap the rest of the pipeline. CORS' OnStarting
// callback runs even on error responses, so headers will still attach.
app.UseAonikExceptionHandler(app.Environment);

app.UseHttpsRedirection();

if (builder.Configuration.GetValue<bool>("Auth:Diagnostics:LogHeaderPresence"))
{
    app.UseAuthHeaderPresenceLogging();
}

app.UseRouting();
app.UseAonikCors();

// Enable WebSocket upgrades for the voice endpoint at /ai/voice.
// See docs/specifications/022.aonik-voice-realtime.md Phase 1.
//
// KeepAliveInterval — Kestrel sends a ping frame every 30 s while the WebSocket
// is open. The default is 2 minutes, but Azure Container Apps' ingress idles
// inactive WebSockets around the same threshold (and our voice sessions
// regularly have multi-second quiet windows between turns while the LLM is
// thinking + TTS hasn't started). Without the explicit interval we saw code
// 1006 (abnormal closure, no close frame) right after the bot finished its
// first reply. 30 s matches the Voxa sample server's setting verbatim.
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30),
});

app.UseAonikDevelopmentStaticFiles(app.Environment, builder.Configuration);

// CRITICAL: Middleware order matters!
// 1. Authentication (validates JWT, runs OnTokenValidated)
app.UseAuthentication();
// 2. Tenant context resolution
app.UseTenantContext();
// 3. Log-scope enrichment — stamps TenantId / UserId / RequestId /
//    CorrelationId onto every ILogger.BeginScope inside the request,
//    so a single log line in the incident tool shows the full identity
//    context. Runs AFTER UseAuthentication + UseTenantContext (those
//    populate ICurrentUserContext + ITenantContext) and BEFORE the
//    handlers so domain services pick the scope up.
app.UseAonikLogScopeEnrichment();
// 4. Authorization (checks policies/permissions)
app.UseAuthorization();
// 5. Tenant validation (validates tenant status only)
app.UseTenantValidation();

// Verify Plaid webhook signatures before FastEndpoints binds/handles the anonymous
// webhook endpoints (H13). No-op for every other path and in Plaid-simulation mode.
app.UsePlaidWebhookVerification();

// 5. FastEndpoints — global CORS policy applied to all endpoints, validator
//    failures surface as 422 (not 400) to match the service-layer convention
//    where 400 is reserved for malformed requests.
app.UseFastEndpoints(c =>
{
    c.Errors.StatusCode = StatusCodes.Status422UnprocessableEntity;
    c.Endpoints.Configurator = ep => ep.Options(b => b.RequireCors(CorsConfiguration.PolicyName));
});

// 6. AI Playground review + scenario endpoints (minimal-API style; mapped
//    individually so each gets a tailored RouteGroup with its own auth +
//    CORS metadata).
app.MapPlaygroundReview("/ai/playground/review")
    .RequireAuthorization("AdminPolicy")
    .RequireCors(CorsConfiguration.PolicyName);

app.MapPlaygroundScenarios("/ai/playground/scenarios")
    .RequireAuthorization("AdminPolicy")
    .RequireCors(CorsConfiguration.PolicyName);

app.MapPlaygroundScenarioGenerate("/ai/playground/scenarios/generate")
    .RequireAuthorization("AdminPolicy")
    .RequireCors(CorsConfiguration.PolicyName);

app.MapAdminNotificationStreaming("/admin/notifications/stream")
    .RequireAuthorization("AdminPolicy")
    .RequireCors(CorsConfiguration.PolicyName);

// Voice WebSocket — Payabo mobile real-time voice mode.
// AonikAuthenticationSetup honours ?access_token=... for this path because
// Flutter WS upgrades may not forward the Authorization header reliably.
app.MapAonikVoiceEndpoints("/ai/voice")
    .RequireAuthorization("MobileVoicePolicy")
    .RequireCors(CorsConfiguration.PolicyName);

// 7. Scalar API Reference (OpenAPI UI) — must be after routing/FastEndpoints.
app.UseAonikSwagger(builder.Configuration);

app.Run();

// Make the Program class accessible for testing
public partial class Program { }
