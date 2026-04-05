# Customer Insight Generation Pipeline

This document explains how AONIK generates financial insights for a customer, produces an AI summary of those insights, and assembles the User Brief that powers the Personal Finance Assistant.

---

## Overview

The pipeline has **two scheduled jobs** that run sequentially and one **on-demand projector** that assembles everything into a brief:

1. **Snapshot Job** — Crunches raw financial data into a deterministic insight snapshot
2. **AI Summary Job** — Sends the snapshot to an LLM for a human-readable narrative
3. **User Brief Projector** — Assembles snapshot + AI summary + user context into a brief (on-demand, when the assistant starts a session)

```mermaid
flowchart LR
    A[Snapshot Job] --> B[AI Summary Job]
    B --> D[User Brief Projector]
    D --> E[Personal Finance Assistant]
```

---

## Step 1: Generate the Deterministic Snapshot

**What:** Analyse a customer's raw financial data (accounts, transactions, bills, subscriptions, budgets, goals) and produce a structured, deterministic snapshot of their financial situation.

**When:** The `CustomerInsightSnapshotJob` runs on a Quartz schedule. It processes users in batches using a checkpoint cursor (tenant + user ID) so it can resume across runs.

**Where the code lives:**
- Job: `src/Aonik.Worker/Jobs/CustomerInsightSnapshotJob.cs`
- Service: `src/Aonik.Finance/Services/PersonalFinance/CustomerInsightSnapshotService.cs`
- Generator: `src/Aonik.Finance/Services/PersonalFinance/CustomerInsightSnapshotGenerator.cs`
- Models: `src/Aonik.Finance/Contracts/Models/PersonalFinance/CustomerInsightSnapshotModels.cs`

### What the snapshot captures

The generator looks at the customer's data across multiple time windows:

| Window | Duration | Purpose |
|--------|----------|---------|
| Operational | 30 days | Current spending, balances, bills due |
| Trend | 90 days | Month-over-month changes |
| Behaviour | 180 days | Longer-term patterns and habits |
| Obligations lookahead | 30 days | Upcoming bills and subscriptions |

It computes these metrics:

- **Cash position** — Balances across accounts, concentration risk
- **Income summary** — Sources, recurring estimates, month-over-month deltas
- **Expense summary** — Fixed vs variable, essential vs discretionary
- **Category insights** — Top spending categories, trends, concentration
- **Merchant insights** — Top merchants, recurring candidates
- **Obligation insights** — Upcoming bills, subscription coverage ratios
- **Budget insights** — Active budgets, categories exceeding thresholds
- **Goal progress** — Tracking against savings/financial goals
- **Behavioural signals** — Key patterns with severity (Low/Moderate/High/Critical) and confidence (Low/Medium/High)
- **Risk overview** — Cashflow stress, budget pressure, concentration risk, missed obligations, unusual activity

### Deduplication

The generator computes a **source hash** (SHA) from a canonical envelope of the input data. If the hash, time window, and generator version match an existing snapshot, it returns the cached version instead of creating a duplicate.

```mermaid
flowchart TD
    A[Quartz triggers CustomerInsightSnapshotJob] --> B[Get next batch of users via checkpoint cursor]
    B --> C{Any users in batch?}
    C -->|No| D[Clear checkpoint, done]
    C -->|Yes| E[For each user in batch]
    E --> F[Set tenant context]
    F --> G[Call GenerateCurrentSnapshotAsync]
    G --> H[Load accounts, transactions, bills, subscriptions, budgets, goals]
    H --> I[Normalize transactions: transfers, categories, account names]
    I --> J[Compute all metrics across time windows]
    J --> K[Compute source hash from canonical envelope]
    K --> L{Hash + window + version match existing?}
    L -->|Yes| M[Return cached snapshot]
    L -->|No| N[Store new snapshot as 'Current', mark previous as 'Superseded']
    N --> O[Log result]
    M --> O
    O --> P{More users?}
    P -->|Yes| E
    P -->|No| Q{Batch full?}
    Q -->|Yes| R[Write checkpoint for next run]
    Q -->|No| D
```

### Snapshot output schema: `customer_insight_snapshot.v1`

```
CustomerInsightSnapshotDocument
├── TenantId, UserId
├── AsOfUtc, WindowStartUtc, WindowEndUtc
├── Coverage (IsPartial, Warnings, SourceCounts)
├── CashPosition (balances, concentration)
├── IncomeSummary (sources, recurring, MoM delta)
├── ExpenseSummary (fixed/variable, essential/discretionary)
├── Categories[] (name, amount, trend, share)
├── Merchants[] (name, amount, recurring flag)
├── Obligations[] (bills, subscriptions, coverage)
├── Budgets[] (category, budgeted, actual, %)
├── Goals[] (name, target, progress, status)
├── Signals[] (key, category, title, severity, confidence)
├── RiskOverview (cashflow, budget, concentration, missed, unusual)
└── Evidence (transaction counts, confirmed transfers, rule versions)
```

---

## Step 2: Generate the AI Summary

**What:** Send the deterministic snapshot to an LLM to produce a human-readable narrative interpretation — a headline, observations, patterns, recommendations, and conversation starters.

**When:** The `CustomerInsightAiSummaryJob` runs on a Quartz schedule after the snapshot job. It finds snapshots that are due for AI interpretation (new or updated snapshots without a matching current summary).

**Where the code lives:**
- Job: `src/Aonik.Worker/Jobs/CustomerInsightAiSummaryJob.cs`
- Service: `src/Aonik.Ai/Services/CustomerInsightAiSummaryService.cs`
- Reader: `src/Aonik.Ai/Services/CustomerInsightAiSummaryReader.cs`
- Entity: `src/Aonik.Ai/Entities/CustomerInsightAiSummary.cs`
- Models: `src/Aonik.SharedKernel/Abstractions/Ai/CustomerInsightAiSummaryModels.cs`

### How it works

```mermaid
flowchart TD
    A[Quartz triggers CustomerInsightAiSummaryJob] --> B[Get next batch of snapshots needing AI summary]
    B --> C{Any snapshots?}
    C -->|No| D[Clear checkpoint, done]
    C -->|Yes| E[For each snapshot]
    E --> F[Set tenant context]
    F --> G[Call GenerateCurrentSummaryAsync]
    G --> H[Load snapshot JSON]
    H --> I[Resolve AI profile: model, system prompt, user prompt template]
    I --> J[Build narrative version string]
    J --> K{Current summary exists with same narrative version?}
    K -->|Yes| L[Return cached summary]
    K -->|No| M[Start AiRun record for audit trail]
    M --> N[Serialize snapshot to JSON]
    N --> O["Replace {{SNAPSHOT_JSON}} in prompt template"]
    O --> P[Send system prompt + user prompt to LLM via IChatClient]
    P --> Q[Parse LLM response, strip JSON fences if present]
    Q --> R[Deserialize into CustomerInsightAiSummaryDocument]
    R --> S[Validate required fields]
    S --> T{Valid?}
    T -->|Yes| U[Store new summary as 'Current', mark previous as 'Superseded']
    T -->|No| V[Store as 'Failed', keep previous summary as fallback]
    U --> W[Mark AiRun as completed]
    V --> X[Mark AiRun as failed]
```

### Narrative versioning

The AI summary has a **narrative version** string in this format:

```
{schemaVersion}|prompt:{promptName}:{promptVersion}|model:{modelId}
```

Example: `customer_insight_ai_summary.v1|prompt:customer_insight_summary:v1|model:claude-sonnet-4-20250514`

If the model or prompt changes, the narrative version changes, and the next job run regenerates the summary. If nothing has changed, the cached summary is reused.

### AI summary output schema: `customer_insight_ai_summary.v1`

```
CustomerInsightAiSummaryDocument
├── Headline                    — One-line opening insight
├── Summary                     — Narrative interpretation paragraph
├── KeyObservations[]           — Main findings from the data
├── PositivePatterns[]          — Strengths and good habits
├── RiskPatterns[]              — Concerns and warning signs
├── RecommendedFocusAreas[]    — Suggested action areas
├── ConversationSuggestions[]  — Topics for the assistant to raise
├── ReferencedMetrics[]        — Which metric keys the AI cited
└── Caveats[]                  — Data limitations the AI flagged
```

### Failure handling

If the LLM call fails (timeout, bad response, validation failure):
- A `Failed` summary record is stored with the failure reason
- The **previous** `Current` summary is left untouched as a fallback
- The `AiRun` is marked as failed for audit
- The pipeline continues to the next snapshot

---

## Step 3: Assemble the User Brief

**What:** Combine the deterministic snapshot, AI summary, user profile, memory, conversation history, and live financial data into a single compact JSON document for the Personal Finance Assistant.

**When:** On-demand, when the assistant session starts. Called via `GET /ai/user-brief`.

**Where the code lives:**
- Projector: `src/Aonik.Agents/Services/UserBriefProjector.cs`
- Endpoint: `src/Aonik.Agents/Endpoints/GetUserBriefEndpoint.cs`
- Models: `src/Aonik.Agents/Contracts/Models/UserBriefModels.cs`

### Data sources

The projector pulls from **four modules concurrently**:

```mermaid
flowchart TD
    subgraph "Concurrent data retrieval"
        F["Finance Module<br/>(IUserBriefDataProvider)"]
        AI["AI Module<br/>(IUserBriefAiDataProvider)"]
        P["Platform Module<br/>(IUserBriefContextDataProvider)"]
    end

    subgraph "Sequential lookup"
        AG["Agents Module<br/>(AgentsDbContext)"]
    end

    F --> PROJ[UserBriefProjector]
    AI --> PROJ
    P --> PROJ
    AG --> PROJ
    PROJ --> BRIEF[UserBrief]
```

| Source | Module | What it provides |
|--------|--------|-----------------|
| Finance data | `Aonik.Finance` | Accounts, balances, bills, subscriptions, spend summaries, budget pressure, goals, support obligations, corridor countries, **customer insight snapshot projection** |
| AI data | `Aonik.Ai` | Memory entries (identity, communication style, household), **customer insight AI summary** |
| User context | `Aonik.Platform` | Profile (name, email, phone), setup profile (use cases, goals, responsibilities) |
| Conversation history | `Aonik.Agents` | Recent conversation summaries, open loops, recommendation outcomes |

### Assembly sequence

```mermaid
sequenceDiagram
    participant Assistant as Personal Finance Assistant
    participant Endpoint as GET /ai/user-brief
    participant Projector as UserBriefProjector
    participant Finance as Finance Module
    participant AI as AI Module
    participant Platform as Platform Module
    participant Agents as Agents DB

    Assistant->>Endpoint: Request user brief
    Endpoint->>Projector: ProjectAsync(tenantId, userId)

    par Concurrent data retrieval
        Projector->>Finance: GetFinancialDataAsync()
        Finance-->>Projector: Balances, bills, spend, snapshot, goals
        Projector->>AI: GetCurrentMemoryEntriesAsync()
        AI-->>Projector: Memory entries
        Projector->>Platform: GetUserContextDataAsync()
        Platform-->>Projector: Profile + setup answers
    end

    Projector->>AI: GetCurrentCustomerInsightAiSummaryAsync(snapshotId)
    AI-->>Projector: AI summary (headline, observations, etc.)

    Projector->>Agents: Query ConversationSummaries
    Agents-->>Projector: Recent sessions, open loops

    Note over Projector: Assemble all sections
    Note over Projector: Derive cashflow risk
    Note over Projector: Derive data availability
    Note over Projector: Apply token budget truncation

    Projector-->>Endpoint: UserBrief
    Endpoint-->>Assistant: JSON brief
```

### User Brief structure

```mermaid
classDiagram
    class UserBrief {
        UserProfile
        SetupProfile
        FinancialFocus
        CurrentState
        CustomerInsightSnapshot
        CustomerInsightAiInterpretation
        DataAvailability
        CashflowRisk
        BehaviouralInsights
        RecentConversationMemory
        PolicyContext
        GeneratedAt
    }

    class UserProfile {
        PreferredName
        CommunicationStyle
        FinancialPosture
        HouseholdContext
        IncomeRhythm
        PrimaryNeeds
    }

    class CurrentState {
        CashSummary
        UpcomingBills[]
        Subscriptions[]
        SpendSummaries[]
        BudgetPressure[]
    }

    class CustomerInsightSnapshot {
        AsOfUtc, Windows
        IsPartial, CoverageWarnings
        Balances, Inflows, Outflows
        TopCategories, TopMerchants
        ObligationCoverage
        BudgetPressure
        GoalProgress
        KeyBehaviourSignals[]
        RiskFlags[]
    }

    class CustomerInsightAiInterpretation {
        Headline
        Summary
        KeyObservations[]
        RecommendedFocusAreas[]
        ReferencedMetricKeys[]
        Caveats[]
    }

    class DataAvailability {
        IsNewUser
        HasLimitedFinancialData
        Summary
        MissingDataAreas[]
    }

    UserBrief --> UserProfile
    UserBrief --> CurrentState
    UserBrief --> CustomerInsightSnapshot
    UserBrief --> CustomerInsightAiInterpretation
    UserBrief --> DataAvailability
```

### Cashflow risk derivation

The projector calculates a simple risk level from live data:

| Risk Level | Condition |
|-----------|-----------|
| **Low** | Available balance > 2x upcoming obligations, or no obligations |
| **Moderate** | Available balance > 1x upcoming obligations |
| **High** | Available balance < upcoming obligations |

### Behavioural insights

Behavioural insights in the brief come directly from the deterministic snapshot's signals. If the snapshot has no signals, the brief contains no behavioural insights.

### Token budget enforcement

If the serialised brief exceeds the token budget, it truncates in this priority order (least important first):

1. Reduce behavioural insights (5 → 3 → 1 → 0)
2. Reduce conversation history (N → 1 → 0)
3. Trim subscriptions (→ 5)
4. Limit spend categories (→ 3 per currency)
5. Trim AI interpretation arrays (→ 3-5 items each)
6. Trim deterministic snapshot lists (→ 3 items each)

---

## End-to-End Pipeline

```mermaid
flowchart TB
    subgraph "Scheduled Jobs (Quartz)"
        direction TB
        J1["1. CustomerInsightSnapshotJob<br/>Batch processes users"]
        J2["2. CustomerInsightAiSummaryJob<br/>Batch processes snapshots"]
    end

    subgraph "Data Sources"
        ACC[Accounts]
        TXN[Transactions]
        BIL[Bills]
        SUB[Subscriptions]
        BUD[Budgets]
        GOL[Goals]
    end

    subgraph "Generated Artefacts"
        SNAP["CustomerInsightSnapshot<br/>(deterministic metrics + signals)"]
        AISUMM["CustomerInsightAiSummary<br/>(LLM narrative interpretation)"]
    end

    subgraph "On-Demand"
        PROJ["UserBriefProjector<br/>(assembles all data)"]
        BRIEF["UserBrief<br/>(compact JSON for assistant)"]
        ASST["Personal Finance Assistant<br/>(uses brief as session context)"]
    end

    ACC & TXN & BIL & SUB & BUD & GOL --> J1
    J1 --> SNAP
    SNAP --> J2
    J2 --> AISUMM

    SNAP --> PROJ
    AISUMM --> PROJ
    PROJ --> BRIEF
    BRIEF --> ASST
```

---

## Key Design Decisions

1. **Deterministic before AI** — The snapshot is pure computation (no LLM). The AI summary is a separate layer on top. If the AI fails, the deterministic data is still available.

2. **Deduplication at every level** — Snapshots use source hashing; AI summaries use narrative versioning. No redundant work.

3. **Graceful degradation** — If the AI summary fails, the previous one stays as fallback. If no snapshot exists, the brief still assembles from live data. If data is limited, the `DataAvailability` section tells the assistant to be cautious.

4. **Auditability** — Every AI summary is linked to an `AiRun` record, tracing exactly which model, prompt, and input produced it.

5. **Token budget awareness** — The brief auto-truncates to fit within the assistant's context window, shedding low-priority information first.

6. **Batch processing with checkpoints** — Jobs process users/snapshots in configurable batches with persistent cursor checkpoints, so they can resume across runs without reprocessing.
