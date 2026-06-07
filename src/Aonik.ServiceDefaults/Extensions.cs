using System.Net.Http.Headers;
using System.Text;
using Aonik.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.SqlClient;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    // AI / Agent Framework metrics (GenAI semantic conventions)
                    .AddMeter("Aonik.Ai")
                    .AddMeter("Aonik.Ai.Calls")
                    // Retrieval metrics (Qdrant + embedding API) — consumed by
                    // the observability dashboard's Retrieval panel.
                    .AddMeter("Aonik.VectorStore")
                    // Domain counters: payments / invoices / ledger entries
                    // (per-tenant), wired from FinanceMetrics and consumed
                    // by the Finance panel of the observability dashboard.
                    .AddMeter("Aonik.Finance")
                    .AddMeter("*Microsoft.Agents.AI");
            })
            .WithTracing(tracing =>
            {
                // Propagate langfuse.* baggage entries as span attributes on every
                // span so that Langfuse can group traces into sessions and associate
                // them with users. Must be registered before other instrumentation
                // to fire on all spans.
                tracing.AddProcessor(new BaggageSpanProcessor("langfuse.", "aonik."));

                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    // AI / Agent Framework tracing (GenAI semantic conventions)
                    .AddSource("Aonik.Ai")
                    // Retrieval tracing — Qdrant upsert/search + embedding calls.
                    .AddSource("Aonik.VectorStore")
                    // Finance money-action tracing — quote / confirm / capture /
                    // transmit / settle / webhook spans tagged with order.id so
                    // operators can pivot on OrderId in App Insights (Issue #142).
                    .AddSource("Aonik.Finance")
                    .AddSource("*Microsoft.Extensions.AI")
                    .AddSource("*Microsoft.Extensions.Agents*");
            });

        // SQL command text capture is gated to development only — query text
        // can contain PII and adds non-trivial telemetry volume. Production /
        // staging records target + db.name + duration only.
        if (IsDevLikeEnvironment(builder.Environment))
        {
            builder.Services.Configure<SqlClientTraceInstrumentationOptions>(options =>
            {
                options.EnrichWithSqlCommand = (activity, command) =>
                {
                    if (command is System.Data.IDbCommand dbCommand && !string.IsNullOrEmpty(dbCommand.CommandText))
                    {
                        activity.SetTag("db.statement", dbCommand.CommandText);
                    }
                };
            });
        }

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    /// <summary>
    /// Disclosure-sensitive feature gate. Strictly tied to
    /// <see cref="HostEnvironmentEnvExtensions.IsDevelopment"/> (i.e.
    /// ASPNETCORE_ENVIRONMENT == "Development") so that the cloud
    /// <c>dev</c> deployment slot — which is a real environment, not a
    /// developer's machine — does NOT enable PII-bearing SQL command
    /// text capture or any other dev-only instrumentation.
    /// </summary>
    private static bool IsDevLikeEnvironment(IHostEnvironment environment) =>
        environment.IsDevelopment();

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Read OTLP endpoint for Aspire dashboard / collector.
        // NOTE: UseOtlpExporter() (cross-cutting) cannot be mixed with signal-specific
        // AddOtlpExporter() on the same IServiceCollection. Since we need a separate
        // Langfuse trace exporter, we use signal-specific exporters for everything.
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing.AddOtlpExporter("aspire", options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
                })
                .WithMetrics(metrics =>
                {
                    metrics.AddOtlpExporter("aspire", options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
                });
        }

        // Langfuse OTLP exporter — exports traces to Langfuse Cloud via HTTP/protobuf.
        // Requires Langfuse:SecretKey and Langfuse:PublicKey in configuration.
        var langfuseSecretKey = builder.Configuration["Langfuse:SecretKey"];
        var langfusePublicKey = builder.Configuration["Langfuse:PublicKey"];
        var langfuseBaseUrl = builder.Configuration["Langfuse:BaseUrl"] ?? "https://cloud.langfuse.com";

        if (!string.IsNullOrWhiteSpace(langfuseSecretKey) && !string.IsNullOrWhiteSpace(langfusePublicKey))
        {
            var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{langfusePublicKey}:{langfuseSecretKey}"));

            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing.AddOtlpExporter("langfuse", options =>
                    {
                        options.Endpoint = new Uri($"{langfuseBaseUrl.TrimEnd('/')}/api/public/otel/v1/traces");
                        options.Protocol = OtlpExportProtocol.HttpProtobuf;
                        options.Headers = $"Authorization=Basic {authString},x-langfuse-ingestion-version=4";
                    });
                });
        }

        if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            builder.Services.AddOpenTelemetry()
               .UseAzureMonitor();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        // Health endpoints are restricted to local development. The cloud
        // "dev" deployment slot must NOT expose unauthenticated /health
        // and /alive endpoints — see https://aka.ms/dotnet/aspire/healthchecks.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks(HealthEndpointPath);

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
