using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;

namespace Aonik.Api.Configuration;

/// <summary>
/// Per-request log-scope enrichment middleware. Stamps every <see cref="ILogger"/>
/// scope inside the request pipeline with the request's
/// <c>TenantId</c>, <c>UserId</c>, <c>RequestId</c>, and
/// <c>CorrelationId</c> so a single line in the incident-triage tool
/// shows the full identity context regardless of which service emitted
/// the log.
/// </summary>
/// <remarks>
/// <para>
/// Must run AFTER <c>UseAuthentication</c> and <c>UseTenantContext</c>
/// (those populate <see cref="ICurrentUserContext"/> and
/// <see cref="ITenantContext"/>) but BEFORE FastEndpoints / handlers
/// so any log inside a handler picks up the scope. The scope is
/// automatically disposed when the request exits.
/// </para>
/// <para>
/// Application Insights / Langfuse / OTLP all read the structured scope
/// keys directly. The keys match the conventions used by the existing
/// SQL-command-text capture and OTel baggage so the trace explorer can
/// pivot from a log line to the trace and back.
/// </para>
/// </remarks>
public static class LogScopeEnrichmentConfiguration
{
    /// <summary>
    /// Logger category used for scope-enrichment logs. Use a dedicated
    /// category so the scope itself doesn't pollute every other logger's
    /// emitted events when the scope is opened.
    /// </summary>
    private const string LoggerCategoryName = "Aonik.RequestScope";

    /// <summary>
    /// Adds the log-scope enrichment middleware. The middleware reads
    /// <see cref="ITenantContext"/>, <see cref="ICurrentUserContext"/>,
    /// and <see cref="ICorrelationContext"/> from the request scope and
    /// opens an <see cref="ILogger.BeginScope{TState}(TState)"/> with
    /// the four well-known keys. Anything logged downstream — domain
    /// services, EF Core, FastEndpoints — picks up the scope.
    /// </summary>
    public static IApplicationBuilder UseAonikLogScopeEnrichment(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger(LoggerCategoryName);

            // Resolution order:
            //   - TenantId / UserId from the typed contexts populated by
            //     UseAuthentication + UseTenantContext upstream.
            //   - RequestId from HttpContext.TraceIdentifier (the same
            //     id ASP.NET Core stamps on the diagnostic source).
            //   - CorrelationId from ICorrelationContext (typically the
            //     incoming X-Correlation-Id header). Fall back to the
            //     RequestId when the upstream did not send one — better
            //     to repeat the request id than emit an empty key.
            var tenantContext = context.RequestServices.GetService<ITenantContext>();
            var userContext = context.RequestServices.GetService<ICurrentUserContext>();
            var correlationContext = context.RequestServices.GetService<ICorrelationContext>();

            var requestId = context.TraceIdentifier;
            var correlationId = correlationContext?.CorrelationId ?? requestId;

            // BeginScope with a Dictionary<string, object?> is the
            // convention every structured-logging provider (Serilog,
            // App Insights, OTLP) reads as named properties on the
            // log entries inside the scope. Null values are emitted as
            // null so unauthenticated requests still attribute the
            // RequestId / CorrelationId tags consistently.
            using var scope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["TenantId"] = tenantContext?.TenantId,
                ["UserId"] = userContext?.UserId,
                ["RequestId"] = requestId,
                ["CorrelationId"] = correlationId,
            });

            await next();
        });
    }
}
