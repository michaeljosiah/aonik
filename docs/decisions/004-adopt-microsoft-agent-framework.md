# ADR-004: Adopt Microsoft Agent Framework (MAF)

**Status**: Accepted  
**Date**: 2026-02-22  
**Decision Makers**: Development Team  
**Supersedes**: [ADR-001: Custom AI Implementation vs MAF](001-custom-ai-implementation-vs-maf.md)  
**Related**: [Modular Restructuring Plan](../modular-restructuring-plan.md)

## Context

ADR-001 chose a custom AI abstraction layer (`IModelProvider`, `IPromptStore`, `IAgentRuntime`) for v0.1 because MAF was in early preview and the priority was running without API keys. That decision was correct for scaffolding, but the situation has changed:

1. **MAF has reached RC** — `Microsoft.Agents.AI` 1.0.0-rc1 (released 2026-02-20) provides `ChatClientAgent`, `AIAgent`, and agent composition (`AsAIFunction()`). `Microsoft.Agents.AI.Workflows` 1.0.0-rc1 provides `AgentWorkflowBuilder`, sequential/concurrent/handoff patterns.
2. **AONIK is restructuring** — the modular monolith migration (Phase 3) is the right time to adopt MAF, avoiding a second migration later
3. **Multi-agent orchestration is needed** — the master orchestrator and domain agents require agent-as-tool composition, which MAF provides natively via `agent.AsAIFunction()`
4. **MCP servers are a requirement** — MAF integrates with the official MCP C# SDK (`ModelContextProtocol` NuGet) for exposing agents as MCP tools
5. **The proposal pattern maps to MAF middleware** — `DelegatingChatClient` from `Microsoft.Extensions.AI` composes into the `ChatClientAgent`'s pipeline, naturally supporting audit logging and proposal interception
6. **Stub provider is still possible** — `IChatClient` is an interface; a `StubChatClient` implementation preserves the "runs without API keys" requirement

### Current Custom Abstractions (to be replaced)

```csharp
// Custom — will be deleted
public interface IModelProvider
{
    Task<string> GenerateCompletionAsync(string systemPrompt, string userPrompt, CancellationToken ct);
}

public interface IAgentRuntime
{
    Task<AgentResponse> ExecuteAsync(AgentRequest request, CancellationToken ct);
}

public class StubModelProvider : IModelProvider { ... }
```

### MAF Replacements (Actual RC Packages)

```csharp
// NuGet packages:
// Microsoft.Agents.AI 1.0.0-rc1
// Microsoft.Agents.AI.Workflows 1.0.0-rc1
// Microsoft.Extensions.AI 10.3.0 (transitive dep, also direct for middleware)

// MAF's AIAgent base type and ChatClientAgent (from Microsoft.Agents.AI)
using Microsoft.Agents.AI;

// IChatClient, DelegatingChatClient, AIFunctionFactory (from Microsoft.Extensions.AI)
using Microsoft.Extensions.AI;

// Create a domain agent via ChatClientAgent constructor
AIAgent agent = new ChatClientAgent(
    chatClient: chatClient,   // IChatClient with middleware pipeline
    name: "FinanceAgent",
    instructions: "You are the AONIK finance agent...",
    tools: tools);             // List<AITool> from AIFunctionFactory.Create()

// Run the agent
AgentResponse response = await agent.RunAsync("What invoices are overdue?");
Console.WriteLine(response.Text);

// Stream the agent
await foreach (var update in agent.RunStreamingAsync("Summarize revenue"))
    Console.Write(update.Text);

// Tools via AIFunctionFactory
var tool = AIFunctionFactory.Create(
    (Guid invoiceId, CancellationToken ct) => GetInvoiceAsync(invoiceId, ct),
    name: "get_invoice",
    description: "Get invoice details by ID");

// Agent-as-tool composition for master orchestrator
AITool financeTool = financeAgent.AsAIFunction();
```

## Decision

Adopt **Microsoft Agent Framework (MAF)** as the standard AI/agent framework for AONIK, replacing all custom AI abstractions during the modular restructuring (Phase 3).

### What We Adopt

| MAF Component | Package | Replaces | Used For |
|--------------|---------|----------|----------|
| `IChatClient` | `Microsoft.Extensions.AI` 10.3.0 | `IModelProvider` | LLM provider abstraction |
| `ChatClientAgent` / `AIAgent` | `Microsoft.Agents.AI` 1.0.0-rc1 | `IAgentRuntime` + custom `AonikDomainAgent` | Domain agent execution |
| `AIFunctionFactory.Create()` | `Microsoft.Extensions.AI` 10.3.0 | Custom `IAgentTool` | Exposing service methods as agent tools |
| `ApprovalRequiredAIFunction` | `Microsoft.Extensions.AI` 10.3.0 | Custom `ProposalMiddleware` | Human-in-the-loop approval for mutating tools |
| `AgentSession` / `CreateSessionAsync` | `Microsoft.Agents.AI` 1.0.0-rc1 | Custom `ConcurrentDictionary` session | Native conversation history tracking |
| `DelegatingChatClient` | `Microsoft.Extensions.AI` 10.3.0 | _(none)_ | Audit logging middleware pipeline |
| `agent.AsAIFunction()` | `Microsoft.Agents.AI` 1.0.0-rc1 | _(none)_ | Agent-as-tool composition for master orchestrator |
| `McpServerTool.Create()` + MCP C# SDK | `ModelContextProtocol` | _(none)_ | Exposing agents as MCP servers |
| `AgentWorkflowBuilder` / `Workflow` | `Microsoft.Agents.AI.Workflows` 1.0.0-rc1 | `InvoiceInsightWorkflow` | Multi-step agent orchestration |

### What We Keep

- **`StubChatClient`**: Custom `IChatClient` implementation returning placeholder responses (preserves "no API keys" requirement)
- **`IPromptStore` / `FileBasedPromptStore`**: Prompt management is AONIK-specific; MAF doesn't prescribe a prompt storage pattern
- **`AiRun` audit entity**: All AI executions are recorded; MAF middleware writes to this
- **`AiRoutePolicy`**: Provider/model routing is AONIK-specific business logic

### Stub Provider Pattern (Preserved)

```csharp
internal class StubChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken ct = default)
    {
        return Task.FromResult(new ChatResponse(
            new ChatMessage(ChatRole.Assistant,
                "Stub AI response — configure a real provider in appsettings.json")));
    }

    public void Dispose() { }
    public ChatClientMetadata Metadata => new("StubChatClient");
}
```

### Human-in-the-Loop via ApprovalRequiredAIFunction

The proposal pattern uses MAF's built-in `ApprovalRequiredAIFunction` to wrap mutating tools,
requiring human approval before execution. This replaces the earlier custom `ProposalMiddleware`
(`DelegatingChatClient`-based) approach, moving approval gating to the tool level where it
is more precise and idiomatic.

```csharp
// Mutating tools are wrapped with ApprovalRequiredAIFunction
var tools = new List<AITool>
{
    AIFunctionFactory.Create(instance.GetInvoiceAsync, name: "GetInvoice"),       // read — no gate
    new ApprovalRequiredAIFunction(                                                // write — gated
        AIFunctionFactory.Create(instance.CreateInvoiceAsync, name: "CreateInvoice")),
    new ApprovalRequiredAIFunction(
        AIFunctionFactory.Create(instance.IssueInvoiceAsync, name: "IssueInvoice")),
};
```

Mutating tools requiring approval: `CreateInvoice`, `IssueInvoice`, `CancelInvoice`,
`MarkInvoicePaid`, `CreatePaymentIntent`, `CapturePayment`, `CancelPayment`, `CreateLedger`,
`CreateAccount`.

### Audit Pipeline via IChatClient Builder

`AuditMiddleware` is wired into the `IChatClient` pipeline using `.AsBuilder().Use(...)`,
integrating with `IAiRunWriter` for real audit records with token usage tracking:

```csharp
// In AiModule.cs — IChatClient registration with audit pipeline
services.AddScoped<IChatClient>(sp =>
{
    var inner = new StubChatClient(); // or real provider
    return inner.AsBuilder()
        .Use((innerClient, services) =>
            new AuditMiddleware(innerClient, services.GetRequiredService<IAiRunWriter>()))
        .Build(sp);
});
```

### Agent-as-Tool Composition (Master Orchestrator)

The orchestrator discovers domain agents via `IEnumerable<IDomainAgentDescriptor>` and
builds `ChatClientAgent` instances that are composed as tools. MCP tools from
`McpToolProvider` are also integrated. The orchestrator agent is cached with
double-checked locking via `SemaphoreSlim`.

```csharp
// IDomainAgentDescriptor interface (multi-registration pattern)
public interface IDomainAgentDescriptor
{
    string Name { get; }
    string Description { get; }
    string Instructions { get; }
    Task<IReadOnlyList<AITool>> GetToolsAsync(IServiceProvider serviceProvider);
}

// Master orchestrator builds and caches agent with domain + MCP tools
foreach (var descriptor in _descriptors)
{
    var tools = await descriptor.GetToolsAsync(serviceProvider);
    var agent = new ChatClientAgent(chatClient, descriptor.Name, descriptor.Instructions, tools);
    orchestratorTools.Add(agent.AsAIFunction(descriptor.Name, descriptor.Description));
}

// MCP tools integrated alongside domain agent tools
var mcpTools = await _mcpToolProvider.GetAllToolsAsync();
orchestratorTools.AddRange(mcpTools);
```

Session management uses MAF's native `AgentSession`:

```csharp
// Native session management — no in-process ConcurrentDictionary
var session = await orchestratorAgent.CreateSessionAsync(sessionId, cancellationToken);
var response = await orchestratorAgent.RunAsync(message, session, cancellationToken);
```

## Rationale

### Why Now (Not Earlier, Not Later)

- **Earlier was too soon**: MAF was immature, and v0.1 needed stubs. ADR-001 was the right call.
- **Later would be costly**: Restructuring into modules is happening now. Building new module agents with custom abstractions and then migrating to MAF later would double the work.
- **Now is the inflection point**: The modular restructuring creates new module projects from scratch. Phase 3 is greenfield for agent code.

### Benefits

1. **Standard framework**: Aligns with Microsoft's direction for .NET AI/agent development
2. **Multi-provider support**: Built-in support for Azure OpenAI, OpenAI, Anthropic, Ollama, Azure AI Foundry
3. **Agent composition**: `agent.AsAIFunction()` enables clean master-orchestrator-to-domain-agent routing
4. **MCP integration**: First-class MCP server/client support via official SDK
5. **Middleware pipeline**: Clean extensibility for cross-cutting concerns (audit, tenancy, proposals)
6. **Function tools**: `AIFunctionFactory.Create()` turns any C# method into an agent tool with zero boilerplate
7. **Workflow engine**: Graph-based multi-agent workflows with checkpointing and human-in-the-loop
8. **Community & ecosystem**: Growing ecosystem of tools, connectors, and documentation

### Trade-offs

1. **RC packages**: MAF is at 1.0.0-rc1 (not GA yet) — pin versions and test upgrades carefully. The RC status indicates API stability.
2. **Learning curve**: Team needs to learn MAF patterns (agents, middleware, workflows)
3. **Framework coupling**: Harder to switch away from MAF later (acceptable given Microsoft's investment)
4. **Stub complexity**: `StubChatClient` must implement the full `IChatClient` interface (more surface than `IModelProvider`)

## Consequences

### Short-term (Phase 3)

- Delete `IModelProvider`, `StubModelProvider`, `IAgentRuntime` and all custom AI interfaces
- Create `StubChatClient : IChatClient` as the default provider
- Domain agents built with `ChatClientAgent` + `AIFunctionFactory` tools
- MAF middleware handles proposal pattern, audit, tenant context
- All existing AI tests rewritten to use MAF abstractions

### Medium-term (Phases 4-5)

- MCP servers expose domain agents via `McpServerTool.Create()` + `agent.AsAIFunction()`
- Master orchestrator composes domain agents as tools
- MAF Workflows replace custom workflow classes (`InvoiceInsightWorkflow`, etc.)
- Real LLM providers (OpenAI, Azure OpenAI) integrated via MAF provider packages

### Long-term

- New domain modules (Health, Productivity, etc.) follow the same MAF pattern
- External AI tools (Copilot, Claude) can connect to AONIK modules via MCP
- Agent marketplace / plugin system built on MAF's extensibility

## Alternatives Considered

### Alternative 1: Continue with Custom Abstractions

**Rejected because**: Would require building agent composition, MCP integration, middleware, and workflow orchestration from scratch. MAF provides all of these.

### Alternative 2: Semantic Kernel

**Rejected because**: MAF is Microsoft's successor that combines Semantic Kernel + AutoGen. Semantic Kernel is being integrated into MAF. Adopting SK now would mean another migration.

### Alternative 3: LangChain.NET / Other Third-Party

**Rejected because**: Less mature in .NET, no Microsoft backing, no MCP integration, smaller ecosystem.

### Alternative 4: Wait for MAF GA (General Availability)

**Rejected because**: The modular restructuring is happening now. Building custom agent code and migrating later doubles the effort. The `IChatClient` / `AIFunctionFactory` / `ChatClientAgent` APIs are stable enough for our use.

## Implementation Plan

This ADR is implemented across the modular restructuring plan:

- **PR 3.1** (DONE): Scaffold `Aonik.Ai` module with MAF's `IChatClient`, replace `IModelProvider` with `StubChatClient`
- **PR 3.2** (DONE): Scaffold `Aonik.Agents` module with MAF RC packages (`Microsoft.Agents.AI` 1.0.0-rc1, `Microsoft.Agents.AI.Workflows` 1.0.0-rc1). Initial domain agent scaffolding.
- **PR 3.3** (DONE): Finance domain agent + tools via `AIFunctionFactory.Create()`
- **PR 3.4** (DONE): Platform domain agent + tools
- **PR 4.1-4.2** (DONE): MCP servers using MCP C# SDK + `McpServerTool.Create()`
- **PR 5.1-5.2** (DONE): Master orchestrator + MAF Workflows (`AgentWorkflowBuilder`)
- **MAF Best Practices Refactor** (DONE): Aligned agent framework with MAF idioms:
  - Replaced `AonikDomainAgent` base class with `IDomainAgentDescriptor` interface + multi-registration pattern
  - Replaced custom `ProposalMiddleware` with MAF's `ApprovalRequiredAIFunction` for human-in-the-loop
  - Moved `AuditMiddleware` to `Aonik.Ai.Middleware` with `IAiRunWriter` integration and token usage tracking
  - Replaced `IChatClientFactory` with direct `IChatClient` registration using `.AsBuilder().Use(...)` pipeline
  - Rewrote `MasterOrchestratorService` to use `AgentSession` for native session management (eliminates `ConcurrentDictionary` memory leak)
  - Split Finance agent into `finance-agent` (~14 tools) and `financial-life-graph-agent` (~17 tools) for better LLM tool selection
  - Integrated `McpToolProvider` into orchestrator tool set
  - Replaced workflow switch statement with keyed `IWorkflowFactory` services

See [Modular Restructuring Plan](../modular-restructuring-plan.md) for detailed specifications per PR.

## References

- [Microsoft Agent Framework Documentation](https://learn.microsoft.com/en-us/agent-framework/)
- [MAF — Running Agents](https://learn.microsoft.com/en-us/agent-framework/agents/running-agents)
- [MAF — Function Tools](https://learn.microsoft.com/en-us/agent-framework/agents/tools/function-tools)
- [MAF — Tools Overview](https://learn.microsoft.com/en-us/agent-framework/agents/tools/)
- [NuGet: Microsoft.Agents.AI 1.0.0-rc1](https://www.nuget.org/packages/Microsoft.Agents.AI/1.0.0-rc1)
- [NuGet: Microsoft.Agents.AI.Workflows 1.0.0-rc1](https://www.nuget.org/packages/Microsoft.Agents.AI.Workflows/1.0.0-rc1)
- [MAF GitHub Repository](https://github.com/microsoft/agent-framework)
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [ADR-001: Custom AI Implementation vs MAF](001-custom-ai-implementation-vs-maf.md) (superseded by this ADR)
- [Modular Restructuring Plan](../modular-restructuring-plan.md)

## Review Date

This decision should be reviewed when:

- MAF reaches GA and APIs may have changed
- A new major version of MAF is released
- Performance or stability issues arise with MAF in production
- An alternative framework emerges with significantly better capabilities

**Next Review Date**: 2026-08-22 (6 months)
