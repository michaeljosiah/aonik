# AI Observability (OpenTelemetry + Langfuse)

AONIK instruments all AI and agent activity with [OpenTelemetry](https://opentelemetry.io/) following the [GenAI Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/). Traces are exported to any OTLP-compatible backend; **Langfuse** is the default for LLM-specific visualization.

## Architecture Overview

Instrumentation is applied at two levels:

1. **IChatClient pipeline** (`Aonik.Ai`) — `.UseOpenTelemetry()` wraps every LLM call (outermost middleware, before `AuditMiddleware`), emitting `gen_ai.chat` spans with tool-call sub-spans.
2. **MAF Agent wrapping** (`Aonik.Agents`) — each domain agent and the master orchestrator are wrapped with `.AsBuilder().UseOpenTelemetry().Build()`, emitting `invoke_agent <name>` spans per the GenAI conventions.

All instrumentation shares a single source name (`Aonik.Ai`) defined in `AiTelemetry.SourceName` (`src/Aonik.SharedKernel/Abstractions/Ai/AiTelemetry.cs`).

### Trace Flow

```
User request
  └─ invoke_agent master-orchestrator        (MasterOrchestratorService)
       ├─ gen_ai.chat (orchestrator LLM call)
       │    └─ gen_ai.tool_call finance-agent
       │         └─ invoke_agent finance-agent   (domain agent)
       │              └─ gen_ai.chat (domain LLM call)
       │                   ├─ gen_ai.tool_call GetInvoice
       │                   └─ gen_ai.tool_call ListLedgerAccounts
       └─ gen_ai.chat (orchestrator synthesis)
```

### Where Instrumentation Is Applied

| Location | File | What It Does |
|----------|------|-------------|
| IChatClient pipeline | `src/Aonik.Ai/AiModule.cs:90-94` | `.UseOpenTelemetry()` as outermost middleware |
| Orchestrator agent | `src/Aonik.Agents/Framework/MasterOrchestratorService.cs:218-223` | Wraps cached orchestrator with OTel |
| Domain agents | `src/Aonik.Agents/Framework/MasterOrchestratorService.cs:311-316` | Wraps each descriptor's built agent centrally |
| OTel subscriptions | `src/Aonik.ServiceDefaults/Extensions.cs:58-84` | Subscribes to `Aonik.Ai` source + MEAI/MAF wildcards |
| OTLP exporters | `src/Aonik.ServiceDefaults/Extensions.cs:91-148` | Aspire + Langfuse dual exporters |
| Shared constants | `src/Aonik.SharedKernel/Abstractions/Ai/AiTelemetry.cs` | `SourceName` and `EnableSensitiveDataKey` |

## Configuration

### Sensitive Data Control

By default, prompts, responses, tool arguments, and tool results are **excluded** from traces for production safety. This is controlled by:

```json
{
  "AI": {
    "OpenTelemetry": {
      "EnableSensitiveData": false
    }
  }
}
```

Set to `true` in `appsettings.Development.json` to include full prompt/response content in traces during development. The config key is `AI:OpenTelemetry:EnableSensitiveData`.

### Langfuse Configuration

Langfuse is configured via three keys in the `Langfuse` section:

```json
{
  "Langfuse": {
    "PublicKey": "pk-lf-...",
    "SecretKey": "sk-lf-...",
    "BaseUrl": "https://cloud.langfuse.com"
  }
}
```

| Key | Required | Description |
|-----|----------|-------------|
| `Langfuse:PublicKey` | Yes | Langfuse project public key |
| `Langfuse:SecretKey` | Yes | Langfuse project secret key |
| `Langfuse:BaseUrl` | No | Defaults to `https://cloud.langfuse.com`. Set to your self-hosted URL if applicable. |

When both keys are present, the exporter is registered automatically. When absent, no Langfuse exporter is added and traces go only to the Aspire dashboard (or any other configured OTLP endpoint).

### Aspire Dashboard

The Aspire dashboard exporter reads `OTEL_EXPORTER_OTLP_ENDPOINT` (set automatically by the Aspire AppHost) and registers signal-specific OTLP exporters for both traces and metrics.

## Sessions & User Attribution

AONIK propagates **Langfuse session** and **user** identifiers on every span in a trace so that Langfuse groups multi-turn conversations under a single session and attributes them to the authenticated user.

### How It Works

Langfuse recognises two span attributes ([docs](https://langfuse.com/docs/integrations/opentelemetry)):

| OTel Attribute | Langfuse Concept |
|----------------|-----------------|
| `langfuse.session.id` | Session — groups related traces |
| `langfuse.user.id` | User — links traces to a user |

For Langfuse to pick these up reliably, the attributes must appear on **all spans** in the trace, including those created by MEAI's `OpenTelemetryChatClient` and MAF's agent framework which cannot be directly modified.

### BaggageSpanProcessor

.NET's OTel SDK has no built-in `BaggageSpanProcessor` (unlike Python/JS). AONIK provides a custom one in `src/Aonik.ServiceDefaults/BaggageSpanProcessor.cs`:

```csharp
public class BaggageSpanProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity data)
    {
        // Copies baggage entries with "langfuse." prefix into span tags
        foreach (var entry in data.Baggage)
            if (entry.Key.StartsWith("langfuse.", StringComparison.Ordinal)
                && entry.Value is not null)
                data.SetTag(entry.Key, entry.Value);
    }
}
```

This processor is registered **first** in the `.WithTracing()` chain (`src/Aonik.ServiceDefaults/Extensions.cs`) so it runs before other processors. It reads `Activity.Baggage` (which propagates automatically to child activities in the same async context) and copies matching entries as span tags.

### Entry Point Instrumentation

Session and user baggage is set at the two AI entry points:

| Entry Point | Session Source | File |
|-------------|---------------|------|
| `POST /ai/chat` (REST) | `ChatRequest.SessionId` | `src/Aonik.Agents/Framework/MasterOrchestratorService.cs` |
| `POST /ai/agui` (AG-UI SSE) | `AguiRunInput.ThreadId` | `src/Aonik.Agents/Endpoints/AguiStreamingEndpoint.cs` |

Both entry points:
1. Resolve the session ID (from request or generate a new GUID)
2. Resolve the user ID via `ICurrentUserProvider.TryGetCurrentUserId()`
3. Set `Activity.Current.SetBaggage()` and `Activity.Current.SetTag()` using `AiTelemetry.SessionIdAttribute` / `AiTelemetry.UserIdAttribute` constants

The baggage propagates down to all child spans (LLM calls, domain agent invocations, tool calls) where the `BaggageSpanProcessor` copies them into tags automatically.

### Constants

Session/user attribute names are defined in `src/Aonik.SharedKernel/Abstractions/Ai/AiTelemetry.cs`:

```csharp
public const string SessionIdAttribute = "langfuse.session.id";
public const string UserIdAttribute = "langfuse.user.id";
```

## Swapping Langfuse for Another OTLP Backend

The Langfuse integration is a standard OTLP HTTP exporter with custom auth headers. You can replace it with **any OTLP HTTP-compatible backend** by modifying `AddOpenTelemetryExporters()` in `src/Aonik.ServiceDefaults/Extensions.cs`.

### Examples

**Honeycomb:**

```csharp
tracing.AddOtlpExporter("honeycomb", options =>
{
    options.Endpoint = new Uri("https://api.honeycomb.io/v1/traces");
    options.Protocol = OtlpExportProtocol.HttpProtobuf;
    options.Headers = $"x-honeycomb-team={honeycombApiKey}";
});
```

**Grafana Tempo (via Grafana Cloud):**

```csharp
tracing.AddOtlpExporter("tempo", options =>
{
    options.Endpoint = new Uri("https://tempo-us-central1.grafana.net/tempo");
    options.Protocol = OtlpExportProtocol.HttpProtobuf;
    var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{instanceId}:{apiKey}"));
    options.Headers = $"Authorization=Basic {auth}";
});
```

**Jaeger (local):**

```csharp
tracing.AddOtlpExporter("jaeger", options =>
{
    options.Endpoint = new Uri("http://localhost:4318/v1/traces");
    options.Protocol = OtlpExportProtocol.HttpProtobuf;
});
```

**Datadog (via OTLP ingest):**

```csharp
tracing.AddOtlpExporter("datadog", options =>
{
    options.Endpoint = new Uri("https://trace.agent.datadoghq.com/api/v0.2/traces");
    options.Protocol = OtlpExportProtocol.HttpProtobuf;
    options.Headers = $"DD-API-KEY={datadogApiKey}";
});
```

### Key Constraint

`UseOtlpExporter()` (cross-cutting) and signal-specific `AddOtlpExporter()` **cannot be mixed** on the same `IServiceCollection`. AONIK uses signal-specific exporters exclusively to support multiple OTLP destinations. If you add a new exporter, continue using the `AddOtlpExporter("name", ...)` pattern.

## OTel Source and Meter Subscriptions

The following sources and meters are subscribed to in `ServiceDefaults/Extensions.cs`:

**Tracing:**
- `Aonik.Ai` — all AONIK AI instrumentation
- `*Microsoft.Extensions.AI` — MEAI `IChatClient` pipeline spans
- `*Microsoft.Extensions.Agents*` — MAF agent framework spans
- Application name source, ASP.NET Core, HttpClient

**Metrics:**
- `Aonik.Ai` — AI-specific metrics
- `*Microsoft.Agents.AI` — MAF agent metrics
- ASP.NET Core, HttpClient, .NET Runtime

## Verifying the Setup

1. Start the Aspire AppHost: `dotnet run --project src/Aonik.AppHost`
2. Open the Aspire dashboard (URL shown in console output)
3. Send an AI chat message via the Admin UI or API
4. Check the Aspire dashboard **Traces** tab — you should see `invoke_agent master-orchestrator` spans with nested `gen_ai.chat` and domain agent sub-spans
5. Check [Langfuse Cloud](https://cloud.langfuse.com) — the same traces appear with LLM-specific visualization (token usage, latency, prompt/completion content if sensitive data is enabled)

## Related Documentation

- [AI Integration](ai-integration.md) — Agent framework architecture, domain agents, audit pipeline
- [Microsoft Agent Framework Observability](https://learn.microsoft.com/en-us/agent-framework/agents/observability?pivots=programming-language-csharp) — Official MAF observability docs
- [Langfuse OpenTelemetry Integration](https://langfuse.com/docs/integrations/opentelemetry) — Langfuse OTel setup guide
- [GenAI Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/) — OTel GenAI span naming
