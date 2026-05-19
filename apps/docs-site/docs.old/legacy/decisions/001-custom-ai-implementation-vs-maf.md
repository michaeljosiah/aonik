:::warning Legacy content

This page predates the docs rewrite. It may be inaccurate or out of date. See the current sidebar for the new home of this topic.

:::

<!-- LEGACY_BANNER -->

# ADR-001: Custom AI Workflow Implementation vs Microsoft Agent Framework

## Status
Accepted

## Context
The v0.1 requirements specified using **Microsoft Agent Framework (MAF)** for AI agents, prompts, and workflows. However, during implementation, we discovered that MAF is:

1. **Still in preview** (prerelease packages as of January 2025)
2. **Requires Azure OpenAI** or compatible IChatClient infrastructure
3. **Tightly coupled** to specific patterns (AIAgent, RunAsync, streaming)
4. **Not suitable for v0.1 stub implementation** where we want to run without API keys

The requirements document states:
> "Make AI provider a stub by default (`IModelProvider` returns placeholder) so the repo runs without secrets"

## Decision
We will implement a **custom AI workflow abstraction layer** that:

1. **Follows MAF-compatible patterns** (agents, prompts, workflows)
2. **Uses our own interfaces** (`IModelProvider`, `IPromptStore`, `IAgentRuntime`)
3. **Supports stub providers** for local development without API keys
4. **Can be migrated to MAF** when ready for production

### Current Implementation

```csharp
// Custom abstraction
public interface IModelProvider
{
    Task<string> GenerateCompletionAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}

// Stub implementation (no API keys required)
public class StubModelProvider : IModelProvider
{
    public Task<string> GenerateCompletionAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        return Task.FromResult("This is a placeholder AI response...");
    }
}

// Workflow orchestration
public class InvoiceInsightWorkflow
{
    private readonly IPromptStore _promptStore;
    private readonly IModelProvider _modelProvider;
    private readonly IAonikDbContext _dbContext;

    public async Task<Insight> ExecuteAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        // 1. Load invoice
        // 2. Load prompts
        // 3. Call AI provider
        // 4. Create and save insight
    }
}
```

### MAF Approach (for reference)

```csharp
// MAF approach (requires Azure OpenAI + API keys)
AIAgent agent = new AzureOpenAIClient(new Uri("https://..."), new AzureCliCredential())
    .GetChatClient("gpt-4o-mini")
    .CreateAIAgent(instructions: "You are good at analyzing invoices.", name: "InvoiceAnalyzer");

var result = await agent.RunAsync("Analyze this invoice...");
```

## Rationale

### Advantages of Custom Implementation
1. **✅ Works without API keys** - Critical for v0.1 and local development
2. **✅ Simple and focused** - No dependency on preview packages
3. **✅ Testable** - Easy to mock and test workflows
4. **✅ Flexible** - Can integrate with any AI provider (OpenAI, Anthropic, Azure, local models)
5. **✅ Migration path** - Interfaces can wrap MAF later

### Disadvantages
1. **❌ Not using official framework** - Diverges from MAF patterns
2. **❌ Manual workflow orchestration** - No built-in agent runtime
3. **❌ Missing MAF features** - No multi-agent coordination, function calling, etc.

### Why This Is Acceptable for v0.1
- **v0.1 is scaffolding** - Requirements state "scaffold only (interfaces, folder structure, sample workflow)"
- **Stub provider is required** - Requirements explicitly call for placeholder responses
- **Production features deferred** - MAF integration can be added post-v0.1
- **Architecture preserved** - Folder structure follows MAF conventions (Agents/, Prompts/, Workflows/)

## Consequences

### Short-term (v0.1)
- ✅ Solution builds and runs without Azure OpenAI
- ✅ Tests pass without external dependencies
- ✅ Developers can work locally without API keys
- ✅ AI workflows demonstrate end-to-end integration

### Medium-term (v0.2+)
- **When real AI is needed**, create MAF-based implementation:
  ```csharp
  public class MafModelProvider : IModelProvider
  {
      private readonly AIAgent _agent;
      
      public async Task<string> GenerateCompletionAsync(...)
      {
          return await _agent.RunAsync(...);
      }
  }
  ```
- Register MAF provider in production: `services.AddScoped<IModelProvider, MafModelProvider>()`
- Stub provider remains for testing: `services.AddScoped<IModelProvider, StubModelProvider>()`

### Long-term
- **If MAF proves valuable**, migrate fully to MAF patterns
- **If custom is sufficient**, keep current architecture
- **Decision deferred** until real AI requirements are clear

## Alternatives Considered

### Alternative 1: Full MAF Integration Now
**Rejected because:**
- Requires Azure OpenAI setup (violates "stub by default" requirement)
- Adds complexity and preview package dependencies
- Blocks local development without API keys
- Not suitable for v0.1 scaffolding phase

### Alternative 2: No AI Abstraction
**Rejected because:**
- Doesn't demonstrate AI-first architecture
- No scaffolding for future AI integration
- Doesn't show workflow patterns

### Alternative 3: Semantic Kernel
**Rejected because:**
- Different framework than specified in requirements
- Same API key requirements as MAF
- Would still need stub provider for v0.1

## Implementation Notes

### Current Structure
```
src/Aonik.Application/Services/Ai/
├── Agents/                  (placeholder for future agent definitions)
├── Prompts/                 (prompt name constants)
└── Workflows/
    └── InvoiceInsightWorkflow.cs

src/Aonik.Infrastructure/Ai/
├── Providers/
│   └── StubModelProvider.cs
└── Prompting/
    ├── FileBasedPromptStore.cs
    └── Templates/
        ├── invoice_insight.v1.system.md
        └── invoice_insight.v1.user.md
```

### Migration Path to MAF
When ready to use MAF:

1. Install MAF packages:
   ```bash
   dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
   dotnet add package Azure.AI.OpenAI --prerelease
   ```

2. Create MAF wrapper:
   ```csharp
   public class MafModelProvider : IModelProvider
   {
       private readonly AIAgent _agent;
       
       public MafModelProvider(IChatClient chatClient)
       {
           _agent = chatClient.CreateAIAgent(
               instructions: "AI agent instructions",
               name: "AgentName");
       }
       
       public async Task<string> GenerateCompletionAsync(string system, string user, CancellationToken ct)
       {
           var messages = new[] 
           {
               new ChatMessage(ChatRole.System, system),
               new ChatMessage(ChatRole.User, user)
           };
           var response = await _agent.RunAsync(messages, ct);
           return response.Text;
       }
   }
   ```

3. Register in DI:
   ```csharp
   services.AddSingleton<IChatClient>(sp =>
   {
       var config = sp.GetRequiredService<IConfiguration>();
       return new AzureOpenAIClient(
           new Uri(config["AzureOpenAI:Endpoint"]),
           new AzureCliCredential())
               .GetChatClient(config["AzureOpenAI:DeploymentName"]);
   });
   
   services.AddScoped<IModelProvider, MafModelProvider>();
   ```

## Review Date
This decision should be reviewed when:
- MAF reaches stable release (not preview)
- Real AI features are being implemented
- Multi-agent coordination is needed
- Advanced MAF features (function calling, streaming, etc.) are required

## References
- [MAF Documentation](https://learn.microsoft.com/en-us/agent-framework/)
- [MAF Tutorial - Run Agent](https://learn.microsoft.com/en-us/agent-framework/tutorials/agents/run-agent)
- AONIK Requirements Document (v0.1) - Section "AI Scaffolding Requirements"
