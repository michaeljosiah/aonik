# AI Integration

AONIK includes a dedicated AI module (`Aonik.Ai`) and an Agents module (`Aonik.Agents`) that together provide AI-native capabilities across the platform.

## Current state

- **Microsoft Agent Framework (MAF)** is adopted for agent orchestration (via `Microsoft.Extensions.AI` / `IChatClient`).
- AI providers, models, and routing policies are managed as platform entities.
- Prompts and tools are versioned and immutable once published.
- Every AI execution is recorded as an `AiRun` for audit and governance.
- Agents follow the **Propose → Approve → Apply** pattern and cannot directly mutate financial state.
- MCP (Model Context Protocol) servers are available per domain module (`Aonik.Finance.Mcp`, `Aonik.Platform.Mcp`).

## Where to look

### AI Module (`Aonik.Ai`)
- Entities: `src/Aonik.Ai/Entities/` (AiProvider, AiModel, AiRoutePolicy, PromptSpec, ToolSpec, AiRun, etc.)
- Persistence: `src/Aonik.Ai/Persistence/`
- Services: `src/Aonik.Ai/Services/`

### Agents Module (`Aonik.Agents`)
- Entities: `src/Aonik.Agents/Entities/` (Agent, AgentRun, Proposal, OrchestratorPolicy)
- Persistence: `src/Aonik.Agents/Persistence/`
- Services: `src/Aonik.Agents/Services/`

### MCP Servers
- Finance MCP: `src/Aonik.Finance.Mcp/`
- Platform MCP: `src/Aonik.Platform.Mcp/`

## Key architectural decisions

- [ADR-001: Custom AI → MAF adoption](../decisions/001-custom-ai-implementation-vs-maf.md)
- [ADR-005: Modular monolith restructuring](../decisions/005-modular-monolith-restructuring.md)
