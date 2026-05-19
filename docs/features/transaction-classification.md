# Transaction Classification

The Transaction Classification system (within the Finance module's Personal Finance subdomain) provides intelligent, multi-tier categorisation of bank transactions for Payabo users. It combines deterministic rule matching, AI-powered fallback classification, and manual user overrides.

## Scope

- 26-category purpose-driven taxonomy with ~90 subcategories
- Scope-aware categorisation rules (System, Tenant, User)
- ~200 pre-seeded system rules for UK + Africa (Ghana, Nigeria, Kenya) merchants
- AI (LLM) classification fallback via `IChatClient`
- Manual override with optional rule creation from corrections
- Review queue for unclassified transactions
- Plaid category mapping with subcategory refinement

## Classification Pipeline

When a user accepts a transaction for classification (`POST /personal-finance/classification/review/{id}/accept`), the system executes a three-step pipeline:

```
┌─────────────────────┐
│  1. Rule Engine      │  Scope-aware: User (0.9) > Tenant (0.8) > System (0.8)
│  (deterministic)     │  Pattern matching: contains, exact, startswith, endswith, regex, amount_range
└────────┬────────────┘
         │ no match
         ▼
┌─────────────────────┐
│  2. AI Classifier    │  LLM via IChatClient, confidence capped at 0.7
│  (fallback)          │  Prompt: transaction_classification.v1
└────────┬────────────┘
         │ no match / error
         ▼
┌─────────────────────┐
│  3. Manual Fallback  │  Category = "uncategorized", Confidence = 0
│  (last resort)       │
└─────────────────────┘
```

### Confidence Hierarchy

| Source              | Confidence | CategorisedBy | ClassificationMethod |
|---------------------|------------|---------------|----------------------|
| Manual override     | 1.0        | `manual`      | `manual`             |
| User rule match     | 0.9        | `rule`        | `rule_engine`        |
| System/Tenant rule  | 0.8        | `rule`        | `system_rule`        |
| AI (LLM)           | 0.0 - 0.7  | `ai`          | `ai_llm`             |
| Provider (Plaid)    | 0.55       | `provider`    | `provider`           |
| Uncategorized       | 0.0        | `manual`      | `manual_fallback`    |

## Category Taxonomy (26 Categories)

Categories are organised into 8 groups:

| Group      | Categories |
|------------|-----------|
| Income     | `income` |
| Transfers  | `transfer_in`, `transfer_out`, `family_support` |
| Essentials | `housing`, `groceries`, `eating_out`, `transport`, `bills`, `health`, `education` |
| Shopping   | `shopping`, `personal_care`, `gifts` |
| Lifestyle  | `entertainment`, `subscriptions`, `travel`, `fitness`, `pets` |
| Financial  | `savings`, `investments`, `loan_payments`, `bank_fees` |
| Services   | `charity` |
| Other      | `other`, `uncategorized` |

### Subcategories

~90 subcategories are stored as in-memory reference data (no database table). Each belongs to a parent category. Examples:

- `groceries` -> `supermarket`, `market`, `online_grocery`, `alcohol`
- `transport` -> `fuel`, `public_transit`, `ride_hailing`, `parking`, `car_maintenance`, `tolls`
- `bills` -> `electricity`, `water`, `gas`, `phone`, `internet`, `insurance`, `council_tax`, `waste`, `tv_licence`
- `family_support` -> `remittance`, `family_allowance`, `school_fees`, `medical_support`

Subcategories are **auto-assigned by the system** (via rules or AI) and displayed in the UI as contextual detail (e.g., "Groceries - Supermarket"). Users select from the 26 top-level categories when overriding; they do not choose subcategories.

Full taxonomy served via `GET /personal-finance/categories`.

## Rule Engine

### Rule Scoping

Rules are scoped with a priority cascade: **User > Tenant > System**.

| Scope    | TenantId       | UserId         | Confidence |
|----------|---------------|----------------|------------|
| `User`   | current tenant | current user   | 0.9        |
| `Tenant` | current tenant | `Guid.Empty`   | 0.8        |
| `System` | `Guid.Empty`   | `Guid.Empty`   | 0.8        |

The query uses `IgnoreQueryFilters()` to load system rules (which have `TenantId = Guid.Empty`), then sorts by scope priority and descending `Priority` within each scope. First match wins.

### Match Types

- `contains` — pattern found anywhere in description/merchant/notes
- `exact` — exact string match
- `startswith` / `endswith` — prefix/suffix match
- `regex` — regular expression (250ms timeout)
- `amount_range` — MinAmount/MaxAmount bounds

### Pre-seeded System Rules

~200 system rules cover common merchants across UK and African markets:

- **UK**: Tesco, Sainsbury's, ASDA, Lidl, Greggs, Costa, TfL, Uber, Netflix, Spotify, Amazon, etc.
- **Ghana**: Melcom, Shoprite GH, MaxMart, Kofi Brokeman, MTN MoMo, Vodafone Cash, etc.
- **Nigeria**: Shoprite NG, Spar, Chicken Republic, Bolt, OPay, Kuda, Jumia, Flutterwave, etc.
- **Kenya**: Naivas, Carrefour Kenya, KFC Kenya, M-Pesa, Safaricom, Jumia Kenya, etc.

Seeded via EF Core `HasData` in `CategorisationRuleConfiguration` using deterministic GUIDs derived from `MD5("system-rule:{category}:{pattern}")`.

### Global Entity Override

System rules use `TenantId = Guid.Empty`. To prevent the `AonikDbContextBase.EnforceTenantOnWrites` interceptor from overwriting this, `FinanceDbContext` overrides `IsGlobalEntity()` to return `true` for `CategorisationRule` entities where `Scope == "System"`.

## AI Classifier

When rules produce no match, the AI classifier (`TransactionAiClassifier`) calls an LLM:

1. Builds a `TransactionInput` DTO (Id, Merchant, Description, Amount, Currency, TransactionType — no PII)
2. Loads prompts from `IPromptStore` (`transaction_classification.v1.system` + `transaction_classification.v1.user`)
3. Records an `AiRun` via `IAiRunWriter`
4. Sends messages to `IChatClient.GetResponseAsync()`
5. Parses JSON response, validates category/subcategory against `TransactionCategoryReference`
6. Clamps confidence to `[0, 0.7]`
7. Marks `AiRun` as completed or failed

Batch classification is supported (`ClassifyBatchAsync`) with a max batch size of 50.

### Prompt Design

- **System prompt**: Lists all 26 categories and ~90 subcategories with descriptions and African market context (M-Pesa, MTN MoMo, OPay, PiggyVest, etc.). Includes 8 classification rules.
- **User prompt**: Provides transaction JSON and expects a JSON array response with `id`, `category`, `subCategory`, `confidence` fields.

## Plaid Integration

`TransactionCategoryReference.MapPlaidCategoryWithSubCategory()` maps Plaid's two-tier taxonomy to AONIK categories:

- 17 primary Plaid categories mapped (e.g., `FOOD_AND_DRINK` -> `groceries`, `MEDICAL` -> `health`)
- 45 detailed Plaid categories provide subcategory refinement (e.g., `FOOD_AND_DRINK.FOOD_AND_DRINK_COFFEE` -> `eating_out` / `cafe`)

## API Surface

### Taxonomy

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/personal-finance/categories` | Anonymous | Full category + subcategory taxonomy |

### Review Queue

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/personal-finance/classification/review-queue` | User | Pending transactions |
| POST | `/personal-finance/classification/review/{id}/accept` | User | Run pipeline & accept result |
| POST | `/personal-finance/classification/review/{id}/override` | User | Manual override (optional rule creation) |

### Categorisation Rules

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/personal-finance/classification/rules` | User | List user's rules |
| POST | `/personal-finance/classification/rules` | User | Create rule |
| PATCH | `/personal-finance/classification/rules/{id}` | User | Update rule |
| POST | `/personal-finance/classification/rules/{id}/deactivate` | User | Deactivate rule |

### Override with Rule Creation

When overriding, setting `CreateRuleFromCorrection = true` auto-creates a User-scoped rule from the correction:

```json
{
  "category": "groceries",
  "notes": "This is my local shop",
  "createRuleFromCorrection": true,
  "rulePattern": "CORNER SHOP",
  "rulePriority": 100,
  "ruleMatchType": "contains"
}
```

## Flutter Integration

### Data Models

- `SpendingTransaction`, `SpendingRecentTransaction`, `PersonalTransactionItem` all carry a `subCategory` field
- `LiveSpendingRepository` parses `subCategory` from API JSON responses
- Mock repositories provide subcategory values for all seed data entries

### UI Display

Subcategories display as contextual detail alongside the parent category:

- Transaction lists: "Groceries - Supermarket"
- Transaction detail: Category pill shows parent; subcategory shown as secondary text in the category card
- Category override: User selects from 26 top-level categories only; subcategory resets to `null`

### Display Name Resolution

`subCategoryDisplayName()` in `category_selection_sheet.dart` provides human-readable names for all ~90 subcategory codes. `categoryDisplayName()` covers all 26 top-level categories.

## Where to Look

### Backend

Files moved to `Aonik.PersonalFinance` per [ADR-006](../decisions/006-extract-personal-finance-module.md):

- **Taxonomy**: `src/Aonik.PersonalFinance/Services/PersonalFinance/TransactionCategoryReference.cs`
- **Pipeline**: `src/Aonik.PersonalFinance/Services/PersonalFinance/TransactionClassificationService.cs`
- **AI Classifier**: `src/Aonik.PersonalFinance/Services/PersonalFinance/TransactionAiClassifier.cs`
- **Seed Rules**: `src/Aonik.PersonalFinance/Services/PersonalFinance/SystemCategorisationRuleSeed.cs`
- **Entities**: `src/Aonik.PersonalFinance/Entities/PersonalFinance/CategorisationRule.cs`, `PersonalTransaction.cs`
- **DTOs**: `src/Aonik.PersonalFinance/Contracts/Models/PersonalFinance/PersonalFinanceModels.cs`
- **Endpoints**: `src/Aonik.PersonalFinance/Endpoints/PersonalFinance/`
- **EF Config**: `src/Aonik.PersonalFinance/Persistence/Configurations/PersonalFinance/`
- **Prompts**: `src/Aonik.Ai/Prompting/Templates/transaction_classification.v1.*.md`
- **Migration**: `src/Aonik.Infrastructure/Persistence/Migrations/20260320185808_AddTransactionClassificationSystemEnhancements.cs`

### Flutter

- **Repositories**: `apps/payabo_mobile/lib/data/repositories/spending_repository.dart`, `personal_transactions_repository.dart`, `live_spending_repository.dart`
- **Mock Data**: `apps/payabo_mobile/lib/mock/repositories/mock_spending_repository.dart`, `mock_personal_transactions_repository.dart`
- **UI**: `apps/payabo_mobile/lib/features/spending/presentation/` (transaction_detail_screen, spending_screen, spending_overview_screen, category_selection_sheet)
- **Router**: `apps/payabo_mobile/lib/app/router/app_router.dart`

## Testing

- Service tests: `tests/Aonik.Application.Tests/PersonalFinance/TransactionClassificationServiceTests.cs`
- Plaid mapping tests: `tests/Aonik.Application.Tests/PersonalFinance/PlaidAccountLinkProviderGatewayTests.cs`
- API integration tests: `tests/Aonik.Api.Tests/PersonalFinanceEndpointsTests.cs`

```bash
# Run all classification-related tests
dotnet test --filter "FullyQualifiedName~TransactionClassification"
dotnet test --filter "FullyQualifiedName~PlaidAccountLinkProvider"
dotnet test --filter "FullyQualifiedName~PersonalFinanceEndpoints"
```

## Design Decisions

1. **Subcategories as in-memory reference data** — No separate DB table. Subcategories are pure constants in `TransactionCategoryReference`, served via API, and stored as a nullable `string` on `PersonalTransaction`. This avoids schema churn when adding new subcategories.

2. **Subcategories are system-assigned, not user-selectable** — Users pick from 26 categories; subcategories are auto-assigned by rules or AI and displayed as contextual detail. This keeps the UX simple while providing analytical depth.

3. **Deterministic GUIDs for seed rules** — Using `MD5("system-rule:{category}:{pattern}")` ensures idempotent migrations and prevents duplicate seed data on re-runs.

4. **AI confidence capped at 0.7** — Ensures AI classifications always rank below rule-based matches in confidence, maintaining the trust hierarchy.

5. **Scope-aware rule cascade** — User rules override tenant rules, which override system rules. This allows platform-wide defaults while letting individual users and tenants customise.

6. **AI failures are non-fatal** — The AI classifier is wrapped in try/catch; failures are logged as warnings. The pipeline gracefully falls back to "uncategorized" rather than failing the request.

7. **Global entity override for system rules** — `CategorisationRule` entities with `Scope == "System"` bypass the tenant write interceptor so `TenantId = Guid.Empty` is preserved.
