# ADR-004: Adopt Microsoft Agent Framework (MAF)

**Status**: Accepted  
**Date**: 2026-02-22  
**Decision Makers**: Development Team  
**Supersedes**: [ADR-001: Custom AI Implementation vs MAF](001-custom-ai-implementation-vs-maf.md)  
**Related**: [Modular Restructuring Plan](../modular-restructuring-plan.md)

## Context

ADR-001 chose a custom AI abstraction layer (`IModelProvider`, `IPromptStore`, `IAgentRuntime`) for v0.1 because MAF was in early preview and the priority was running without API keys. That decision was correct for scaffolding, but the situation has changed:

1. **MAF has matured** — `Microsoft.Agents.AI` and `Microsoft.Extensions.AI` packages now provide stable abstractions (`IChatClient`, `ChatClientAgent`, `AIFunctionFactory`)
2. **AONIK is restructuring** — the modular monolith migration (Phase 3) is the right time to adopt MAF, avoiding a second migration later
3. **Multi-agent orchestration is needed** — the master orchestrator and domain agents require agent-as-tool composition, which MAF provides natively via `agent.AsAIFunction()`
4. **MCP servers are a requirement** — MAF integrates with the official MCP C# SDK (`ModelContextProtocol` NuGet) for exposing agents as MCP tools
5. **The proposal pattern maps to MAF middleware** — MAF's `IFunctionInvocationMiddleware` is a natural fit for intercepting high-risk tool calls and creating proposals
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

### MAF Replacements

```csharp
// MAF's IChatClient (from Microsoft.Extensions.AI)
public interface IChatClient
{
    Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken ct);
}

// MAF's ChatClientAgent (from Microsoft.Agents.AI)
var agent = new ChatClientAgent(
    chatClient: chatClient,
    name: "FinanceAgent",
    instructions: "...",
    tools: tools);

// MAF's AIFunctionFactory for tools
var tool = AIFunctionFactory.Create(
    (Guid invoiceId, CancellationToken ct) => GetInvoiceAsync(invoiceId, ct),
    name: "get_invoice",
    description: "Get invoice details by ID");
```

## Decision

Adopt **Microsoft Agent Framework (MAF)** as the standard AI/agent framework for AONIK, replacing all custom AI abstractions during the modular restructuring (Phase 3).

### What We Adopt

| MAF Component | Replaces | Used For |
|--------------|----------|----------|
| `IChatClient` | `IModelProvider` | LLM provider abstraction |
| `ChatClientAgent` / `AIAgent` | `IAgentRuntime` | Domain agent execution |
| `AIFunctionFactory.Create()` | Custom `IAgentTool` | Exposing service methods as agent tools |
| `IFunctionInvocationMiddleware` | _(none)_ | Proposal pattern, audit logging, tenant context |
| `agent.AsAIFunction()` | _(none)_ | Agent-as-tool composition for master orchestrator |
| `McpServerTool.Create()` + MCP C# SDK | _(none)_ | Exposing agents as MCP servers |
| MAF Workflows | `InvoiceInsightWorkflow` | Multi-step agent orchestration |

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

### Proposal Pattern via MAF Middleware

```csharp
internal class ProposalMiddleware : IFunctionInvocationMiddleware
{
    private readonly IProposalService _proposalService;

    public async Task InvokeAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        var riskTier = GetRiskTier(context.Function);

        if (riskTier == RiskTier.High)
        {
            // Block execution — create proposal for human approval
            var proposal = await _proposalService.CreateAsync(
                action: context.Function.Name,
                parameters: context.Arguments);

            context.Result = new FunctionResult(
                $"Proposal {proposal.Id} created. Awaiting human approval.");
            return;
        }

        // Low/medium risk: execute directly
        await next(context);
    }
}
```

### Agent-as-Tool Composition (Master Orchestrator)

```csharp
// Each domain agent becomes a tool the master orchestrator can call
var masterAgent = new ChatClientAgent(
    chatClient: chatClient,
    name: "MasterOrchestrator",
    instructions: "Route user requests to the appropriate domain agent...",
    tools: new List<AITool>
    {
        financeAgent.AsAIFunction("finance", "Handle finance operations"),
        platformAgent.AsAIFunction("platform", "Handle platform operations"),
    });
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

1. **Preview packages**: Some MAF packages are still prerelease — pin versions and test upgrades carefully
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

- **PR 3.1**: Scaffold `Aonik.Ai` module with MAF's `IChatClient`, replace `IModelProvider` with `StubChatClient`
- **PR 3.2**: Scaffold `Aonik.Agents` module with MAF's `ChatClientAgent`, add middleware (proposal, audit, tenant)
- **PR 3.3**: Finance domain agent + tools via `AIFunctionFactory.Create()`
- **PR 3.4**: Platform domain agent + real LLM provider wrappers
- **PR 4.1-4.2**: MCP servers using MCP C# SDK + `McpServerTool.Create()`
- **PR 5.1-5.2**: Master orchestrator + MAF Workflows

See [Modular Restructuring Plan](../modular-restructuring-plan.md) for detailed specifications per PR.

## References

- [Microsoft Agent Framework Documentation](https://learn.microsoft.com/en-us/agent-framework/)
- [MAF Tutorial — Run Agent](https://learn.microsoft.com/en-us/agent-framework/tutorials/agents/run-agent)
- [MAF Tutorial — Agent Tools](https://learn.microsoft.com/en-us/agent-framework/tutorials/agents/agent-tools)
- [MAF Tutorial — Agent as MCP Server](https://learn.microsoft.com/en-us/agent-framework/tutorials/mcp/agent-as-mcp-server)
- [MAF Middleware](https://learn.microsoft.com/en-us/agent-framework/concepts/middleware)
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
