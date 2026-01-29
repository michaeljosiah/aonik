import pathlib
import re
from collections import defaultdict


AUDIT_COLS = {
    "CreatedAt",
    "CreatedBy",
    "UpdatedAt",
    "UpdatedBy",
    "IsDeleted",
    "DeletedAt",
    "DeletedBy",
    "RowVersion",
}


MODULE_ORDER = [
    ("Identity", "Aonik.Domain.Identity."),
    ("Party", "Aonik.Domain.Party."),
    ("Ledger", "Aonik.Domain.Ledger."),
    ("Orders", "Aonik.Domain.Orders."),
    ("Payments", "Aonik.Domain.Payments."),
    ("Billing", "Aonik.Domain.Billing."),
    ("Catalog", "Aonik.Domain.Catalog."),
    ("Partners", "Aonik.Domain.Partners."),
    ("Pricing", "Aonik.Domain.Pricing."),
    ("Compliance", "Aonik.Domain.Compliance."),
    ("Notifications", "Aonik.Domain.Notifications."),
    ("Operations", "Aonik.Domain.Operations."),
    ("Features", "Aonik.Domain.Features."),
    ("Reference Data", "Aonik.Domain.ReferenceData."),
    ("Settings", "Aonik.Domain.Settings."),
    ("AI", "Aonik.Domain.Ai."),
    ("Agents", "Aonik.Domain.Agents."),
    ("Personal Finance", "Aonik.Domain.PersonalFinance."),
    ("Infrastructure", "Aonik.Infrastructure."),
]


def module_for(entity_fqn: str) -> str:
    for name, prefix in MODULE_ORDER:
        if entity_fqn.startswith(prefix):
            return name
    return "Other"


def parse_indexes(raw_list: list[str]) -> list[list[str]]:
    out: list[list[str]] = []
    for raw in raw_list or []:
        cols = re.findall(r"\"([^\"]+)\"", raw)
        if cols:
            out.append(cols)
    return out


def looks_like_join_table(cols: list[dict]) -> bool:
    names = [c["name"] for c in cols if c["name"] not in AUDIT_COLS]
    business = [n for n in names if n not in ("Id", "TenantId")]
    return bool(business) and len(business) <= 3 and all(n.endswith("Id") for n in business)


def key_columns(cols: list[dict]) -> list[str]:
    names = [c["name"] for c in cols]

    def is_business(n: str) -> bool:
        return n not in AUDIT_COLS

    preferred: list[str] = []
    for n in [
        "Id",
        "TenantId",
        "Status",
        "Name",
        "Type",
        "UseCase",
        "RiskTier",
        "OrderType",
        "QuoteType",
        "Currency",
        "CurrencyIn",
        "CurrencyOut",
        "Amount",
        "AmountIn",
        "AmountOut",
        "Total",
        "Subtotal",
        "FeesTotal",
        "ExchangeRate",
        "Rate",
        "DueDate",
        "ExpiresAt",
        "UserId",
        "PartyId",
        "CustomerAccountId",
        "InvoiceId",
        "OrderId",
        "PaymentIntentId",
        "PaymentId",
        "PayoutId",
        "ConnectorId",
        "PartnerId",
    ]:
        if n in names and n not in preferred:
            preferred.append(n)

    extras: list[str] = []
    for n in names:
        if not is_business(n) or n in preferred:
            continue
        if n.endswith("Id") or n.endswith("Json"):
            extras.append(n)
        elif n in ("RetryCount", "LastError", "IsActive", "Severity", "Message", "Title", "Summary"):
            extras.append(n)

    rest = [n for n in names if is_business(n) and n not in preferred and n not in extras]
    ordered = preferred + extras + rest
    return ordered[:12]


PURPOSE: dict[str, tuple[str, str]] = {
    # Identity
    "Tenants": (
        "Top-level container for data isolation. Most business data is tenant-scoped and filtered by this tenant.",
        "Create a Tenant when onboarding a new business/product environment.",
    ),
    "Users": (
        "Human users who can sign in and act in a tenant. Used for authorization, attribution, and approvals.",
        "Create a User when inviting an operator to manage billing, review compliance cases, or approve proposals.",
    ),
    "Roles": (
        "Named permission bundles used to grant access consistently across many users.",
        "Create a role like `OpsReviewer` once, then assign it to multiple users.",
    ),
    "Permissions": (
        "Atomic capabilities the system can authorize (e.g., `invoices:create`, `ledger:post`).",
        "Add a Permission when you ship a new capability and want it assignable via roles.",
    ),
    "UserRoles": (
        "Join table linking users to roles (many-to-many).",
        "When you invite a teammate, create a UserRole row to grant them `BillingAdmin`.",
    ),
    "RolePermissions": (
        "Join table linking roles to permissions (many-to-many).",
        "When you decide `BillingAdmin` can issue invoices, insert a RolePermission linking that role to the permission.",
    ),
    "UserParties": (
        "Bridge between identity (`Users`) and business identity (`Parties`). This is how a login maps to a person/business record.",
        "After onboarding, link the signed-in user to their Person Party so orders/payments can reference the Party.",
    ),
    "VerificationChallenges": (
        "Short-lived verification workflows (OTP/email/SMS/etc.) for sign-in, signup, or sensitive actions.",
        "Create a VerificationChallenge when sending an OTP; mark it used/failed when the code is verified/invalid.",
    ),

    # Party
    "Parties": (
        "Canonical representation of a person or business in the platform (customers, merchants, senders, receivers).",
        "Create a Party when a new customer is onboarded or when an order needs to reference a real-world counterparty.",
    ),
    "PersonProfiles": (
        "Personal details for parties that are people (KYC profile data).",
        "Create a PersonProfile after collecting a user's legal name and identity details during KYC.",
    ),
    "BusinessProfiles": (
        "Business details for parties that are organizations (KYB profile data).",
        "Create a BusinessProfile when onboarding a merchant with registration details.",
    ),
    "PartyAddresses": (
        "Addresses linked to a party (billing, residential, registered).",
        "Add a PartyAddress when a merchant sets a registered address or a customer adds a billing address.",
    ),
    "PartyContacts": (
        "Contact channels for a party (email, phone, etc.) used for verification and notifications.",
        "Add a PartyContact when a customer provides a phone number for payout notifications.",
    ),
    "PartyConsents": (
        "Consent records for a party (what they agreed to, when, and under which policy/version).",
        "Create a PartyConsent when a user accepts Terms of Service or a data-sharing consent.",
    ),
    "ExternalAccounts": (
        "External financial accounts linked to a party (bank account, mobile money wallet, card token reference).",
        "Create an ExternalAccount when a user adds a bank account to fund payments or receive payouts.",
    ),
    "PartyRoleAssignments": (
        "Business roles for parties (e.g., Merchant, Customer, Beneficiary), separate from auth roles.",
        "Assign `Merchant` to a Party to enable billing features and merchant workflows.",
    ),
    "PartyRelationships": (
        "Relationships between two parties (e.g., business owner, parent/child account, employer/employee).",
        "Link a business Party to an owner Person Party with a PartyRelationship.",
    ),

    # Ledger
    "Ledgers": (
        "Accounting boundary/container for a tenant's chart of accounts and journal entries.",
        "Create a Ledger when provisioning a tenant before creating ledger accounts and posting entries.",
    ),
    "LedgerAccounts": (
        "Chart of accounts. Journal entry lines post to these accounts to build balances and financial statements.",
        "Create LedgerAccounts like `Cash`, `Fees Revenue`, `Customer Receivable` so transactions can be posted correctly.",
    ),
    "JournalEntries": (
        "A single accounting event (header) that groups balanced debit/credit lines. This is the proof of financial truth.",
        "When a payment settles, create one JournalEntry to record the accounting impact (cash movement, fees, liabilities).",
    ),
    "JournalEntryLines": (
        "Debit/credit lines belonging to a journal entry. The full set must balance.",
        "Add lines to debit `Cash` and credit `Customer Liability` for a received payment.",
    ),
    "BalanceSnapshots": (
        "Precomputed balances at a point in time for reporting and performance (does not replace journal entries).",
        "Write BalanceSnapshots nightly to power dashboards without recalculating from all journal entry lines.",
    ),

    # Orders
    "Orders": (
        "Business intent hub: why money should move. Orders orchestrate funding and fulfilment without being the payment itself.",
        "Create an Order when a user initiates a bill payment or remittance; link it to funding (PaymentIntents) and fulfilment (Payouts).",
    ),
    "OrderItems": (
        "Components/line items within an order (useful when an order has multiple payable items).",
        "Add OrderItems when paying multiple bills in one checkout.",
    ),
    "OrderPartyRoles": (
        "Explicit party role assignments inside an order (payer, payee, sender, receiver, beneficiary, merchant).",
        "Add OrderPartyRoles to state who is the sender and who is the receiver for a remittance order.",
    ),
    "OrderFundingRefs": (
        "References from an order to its funding objects (typically PaymentIntents).",
        "Create an OrderFundingRef after checkout creates a PaymentIntent to fund the order.",
    ),
    "OrderFulfilmentRefs": (
        "References from an order to fulfilment objects (typically Payouts, partner transmissions, etc.).",
        "Create an OrderFulfilmentRef when a payout is created to deliver money to the beneficiary.",
    ),
    "OrderHistoryEvents": (
        "Append-only timeline of state changes and notable events for an order (auditable narrative).",
        "Add an OrderHistoryEvent when an order moves from `Pending` to `Funded`, or when a partner confirms delivery.",
    ),
    "OrderNotes": (
        "Human-entered notes attached to an order for support/ops context (separate from structured events).",
        "Add an OrderNote when support explains why an order was cancelled or refunded.",
    ),

    # Payments
    "PaymentIntents": (
        "Intent to collect funds from a payer via a funding method (card, bank, wallet). Created before attempts.",
        "Create a PaymentIntent at checkout before calling a payment provider.",
    ),
    "Payments": (
        "Payment execution records (attempts/results) tied back to a PaymentIntent and provider references.",
        "Create a Payment when the payment provider returns an authorization/capture result.",
    ),
    "Payouts": (
        "Outbound fulfilment executions that send money out to a beneficiary (bank/mobile money/etc.).",
        "Create a Payout after an order is funded to deliver money to the receiver via a connector.",
    ),
    "Refunds": (
        "Refund execution records tied to a payment (money returned to the payer).",
        "Create a Refund when an order is cancelled after capture and funds must be returned.",
    ),
    "Chargebacks": (
        "Dispute/chargeback records for payments (provider-initiated reversals and dispute lifecycle).",
        "Create a Chargeback when a card network notifies that a payer disputed a card payment.",
    ),

    # Billing
    "CustomerAccounts": (
        "Billing relationship between a merchant party and a customer party, including preferences and status.",
        "Create a CustomerAccount when a business starts invoicing a specific customer.",
    ),
    "Invoices": (
        "Billable documents issued by a merchant to a customer, tracking totals, due dates, and lifecycle state.",
        "Create an Invoice when generating a bill for a subscription renewal or one-off service.",
    ),
    "InvoiceLines": (
        "Invoice line items (description, quantity, unit price, tax).",
        "Add InvoiceLines like `Internet plan - January` and `Setup fee`.",
    ),
    "InvoiceAllocations": (
        "Allocation records showing how a payment is applied to an invoice (supports partial payments).",
        "Create an InvoiceAllocation when a 50.00 payment is applied to a 100.00 invoice.",
    ),
    "DunningPlans": (
        "Dunning configuration for a customer account (reminders, escalation rules, schedules).",
        "Create a DunningPlan to automate reminders for overdue invoices.",
    ),

    # Catalog
    "CatalogBillerCategories": (
        "Categories used to group billers/services for discovery (e.g., Utilities, Telecom).",
        "Create a CatalogBillerCategory so the UI can group billers consistently.",
    ),
    "CatalogBillers": (
        "Directory of billers available for bill payment, with country and routing metadata.",
        "Add a CatalogBiller when onboarding a new utility/telecom provider into the bill-pay catalog.",
    ),
    "CatalogBillerServices": (
        "Specific payable services under a biller (e.g., prepaid top-up vs postpaid bill).",
        "Add a CatalogBillerService when a biller offers multiple service types.",
    ),

    # Partners
    "Partners": (
        "External partner organizations (processors, correspondents, payout providers) in the network.",
        "Create a Partner for a new payout provider your routing rules can select.",
    ),
    "PartnerBranches": (
        "Partner branch/location metadata used for coverage, routing, and operations.",
        "Add a PartnerBranch when a cash-out partner has regional branches with different capabilities.",
    ),
    "Connectors": (
        "Technical connector definitions for integrating with partners (capabilities and auth references).",
        "Create a Connector when adding a new integration to a payment processor or payout rail.",
    ),
    "RoutingRules": (
        "Rules that choose which partner/connector to use based on corridor, amount, service, risk, etc.",
        "Create a RoutingRule to send NGN mobile money payouts to Provider A but bank payouts to Provider B.",
    ),
    "PayoutSchemas": (
        "Schemas/templates describing required payout fields per corridor/connector for validation and mapping.",
        "Use PayoutSchemas to enforce required fields like bank code/account number for bank payouts.",
    ),
    "Transmissions": (
        "Outbound transmission attempts to partners for fulfilment (tracks status, retries, and last error).",
        "Create a Transmission when sending a payout request to a connector and track retries until success.",
    ),

    # Pricing
    "FeePolicies": (
        "Fee calculation policies (fixed/percentage + conditions) used during quoting and order pricing.",
        "Create a FeePolicy like `Standard Remit Fee` with 1% + 2.00 fixed for specific corridors.",
    ),
    "FxQuotes": (
        "Short-lived FX quotes (rate + expiry) used to price cross-currency orders.",
        "Create an FxQuote when showing the user a conversion rate that expires in a short window.",
    ),
    "LimitsPolicies": (
        "Limit rules (amount caps, velocity limits, corridor restrictions) used for risk/compliance enforcement.",
        "Create a LimitsPolicy to cap transfers per day unless the user has a higher verification level.",
    ),
    "PricingQuotes": (
        "Full pricing results combining FX, fees, and totals for a specific context (a price the user can accept).",
        "Create a PricingQuote during checkout so the user can accept an all-in price before paying.",
    ),

    # Compliance
    "ScreeningChecks": (
        "Screening results (sanctions/PEP/etc.) tied to parties or transactions for compliance gating.",
        "Create a ScreeningCheck when onboarding a party or before executing a payout.",
    ),
    "ComplianceCases": (
        "Case management records for compliance/risk investigations (holds, reviews, escalations).",
        "Create a ComplianceCase when screening flags a party and a human must review before fulfilment.",
    ),
    "AuditLogs": (
        "Audit trail of important system actions (who did what, when, and on which subject).",
        "Write an AuditLog entry when an operator approves a high-risk proposal or changes a policy.",
    ),

    # Features
    "TenantFeatures": (
        "Feature flag state per tenant (enables/disables capabilities safely).",
        "Enable a TenantFeature to roll out a new capability to a pilot tenant first.",
    ),

    # Settings
    "Settings": (
        "Key/value runtime configuration scoped to tenant (and sometimes global) used to control behavior.",
        "Store a Setting like default currency, notification preferences, or integration toggles for a tenant.",
    ),

    # Reference Data
    "ReferenceData": (
        "Curated reference lists (countries, currencies, document types, etc.) used for validation and consistent UX.",
        "Query ReferenceData for ISO country codes when validating payout addresses or KYC forms.",
    ),

    # Notifications
    "Notifications": (
        "Notification records to users/parties (email/SMS/in-app) including delivery status and payload references.",
        "Create a Notification when an invoice is issued or a payout completes and the user should be informed.",
    ),
    "WebhookSubscriptions": (
        "Webhook endpoints registered by external systems that want event callbacks.",
        "Create a WebhookSubscription so a merchant system receives events like `invoice.paid`.",
    ),

    # Operations
    "WorkItems": (
        "Operational tasks/work queue items for humans or automation (triage, reviews, follow-ups).",
        "Create a WorkItem when a payout fails and ops must investigate.",
    ),
    "Jobs": (
        "Batch/scheduled job records tracking long-running operational processes and their status.",
        "Create a Job record when running nightly reconciliation or daily balance snapshot generation.",
    ),

    # AI
    "AiProviders": (
        "Configured AI providers (vendors) with capability metadata and auth references (not raw secrets).",
        "Create an AiProvider when adding a new LLM vendor integration.",
    ),
    "AiModels": (
        "Models available via providers, with cost/latency profiles and policy tags.",
        "Add an AiModel when enabling a new model for a use case.",
    ),
    "AiPolicies": (
        "Safety/governance policies for AI runs (allowed data fields, redaction, escalation rules).",
        "Use an AiPolicy to forbid raw PII in prompts for a specific use case.",
    ),
    "AiRoutePolicies": (
        "Routing policies that select which AI model to use based on use case, risk tier, and sensitivity.",
        "Create an AiRoutePolicy so low-risk tasks use a cheaper model and sensitive tasks use a stricter policy.",
    ),
    "PromptSpecs": (
        "Versioned prompt definitions (templates + schemas) to keep AI behavior reproducible over time.",
        "Create a new PromptSpec version when improving a prompt but keeping old runs reproducible.",
    ),
    "ToolSpecs": (
        "Versioned tool contracts exposed to agents/LLMs (what tools exist and how to call them safely).",
        "Add a ToolSpec when exposing a read-only domain tool like `GetInvoiceById` to the AI platform.",
    ),
    "AiRuns": (
        "Audit record of an AI execution: inputs/outputs by reference, tokens, cost, latency, and outcome.",
        "Create an AiRun whenever the system calls an LLM to classify, summarize, or draft content.",
    ),
    "AiTraces": (
        "Detailed trace of an AI run (steps/tool calls and optional reasoning reference) for debugging and audit.",
        "Store an AiTrace when you need to show exactly which tools were called to produce an output.",
    ),
    "AiFeedbacks": (
        "Human feedback on AI runs (ratings/corrections) for improving prompts and evaluations.",
        "Create AiFeedback when an operator marks an AI result wrong and provides the correction.",
    ),
    "EvalSuites": (
        "Collections of evaluation scenarios/metrics used to test prompts/models systematically.",
        "Create an EvalSuite for a prompt before publishing changes.",
    ),
    "EvalRuns": (
        "Individual evaluation executions and stored results for an evaluation suite.",
        "Create an EvalRun when running prompt/model regression tests in CI.",
    ),
    "Insights": (
        "Generated insights attached to a subject (order, invoice, user, etc.) for UI surfacing and decision support.",
        "Create an Insight like `late payment risk` for a customer account to show in operations UI.",
    ),
    "Signals": (
        "Operational/analytic signals (typed messages with severity) for monitoring and triage.",
        "Write a Signal when repeated payout failures occur for a corridor, triggering ops attention.",
    ),

    # Agents
    "Agents": (
        "Configured domain agents (name, domain, risk tier, toolset) that can propose actions but do not directly mutate financial state.",
        "Create an Agent like `Billing Assistant` that can draft invoice insights and produce proposals.",
    ),
    "AgentRuns": (
        "Execution records for agents (goal, plan/steps, linked AI runs, produced artifacts).",
        "Create an AgentRun when an agent performs `review overdue invoices and propose dunning actions`.",
    ),
    "OrchestratorPolicies": (
        "Policies that decide which agents to use for a given intent type, including preferred and fallback agent sets.",
        "Create an OrchestratorPolicy so `invoice_help` routes to the Billing agent first, then a generic assistant.",
    ),
    "Proposals": (
        "Material action proposals produced by agents/AI, including payload, risk tier, and approval state.",
        "Create a Proposal when an agent suggests a refund or policy change; require approval before applying it.",
    ),

    # Personal Finance
    "PersonalProfiles": (
        "Personal finance profile used by B2C features (preferences and finance-specific settings).",
        "Create a PersonalProfile when a user starts using budgeting and personal finance features.",
    ),
    "Households": (
        "Groupings for shared personal finance (family/shared budgets) with shared artifacts.",
        "Create a Household when two users want a shared budget and shared bills list.",
    ),
    "HouseholdMembers": (
        "Membership table linking personal profiles to households (who belongs to which household).",
        "Add a HouseholdMember when inviting another user into a household budget.",
    ),
    "PersonalTransactions": (
        "Personal finance transaction records (imported or manually entered) used for categorisation and budgeting.",
        "Create a PersonalTransaction when importing a bank statement line item or recording a cash expense.",
    ),
    "CategorisationRules": (
        "Rules to auto-categorize personal transactions based on merchant text, amount ranges, or other heuristics.",
        "Create a CategorisationRule so any transaction containing `Netflix` is categorized as `Subscriptions`.",
    ),
    "Budgets": (
        "Budget containers for a period (monthly/weekly) tied to a profile/household.",
        "Create a Budget at the start of a month for a household.",
    ),
    "BudgetLines": (
        "Budget category lines under a budget with planned amounts.",
        "Add BudgetLines for `Groceries 200` and `Transport 80` for the month.",
    ),
    "Bills": (
        "Bills to pay (one-off or recurring) with due dates and status.",
        "Create a Bill when a user adds their electricity bill with a due date.",
    ),
    "Subscriptions": (
        "Recurring subscription commitments (service, cadence, expected amount) for forecasting and reminders.",
        "Create a Subscription for `Spotify monthly` so it appears in upcoming bills and spend analysis.",
    ),
    "Goals": (
        "Savings or spending goals with targets and timelines.",
        "Create a Goal like `Save 1000 by June` and track progress from personal transactions.",
    ),

    # Infrastructure
    "AonikBackgroundJobRecords": (
        "Internal background job persistence used by the platform runtime (queue/status/retry bookkeeping).",
        "A background worker creates an AonikBackgroundJobRecord when scheduling work like sending invoice reminders.",
    ),
}


def auto_purpose(table: str, entity_fqn: str, cols: list[dict]) -> tuple[str, str]:
    if table in PURPOSE:
        return PURPOSE[table]

    entity = entity_fqn.split(".")[-1]
    col_names = {c["name"] for c in cols}
    mod = module_for(entity_fqn)

    if looks_like_join_table(cols):
        ids = sorted([n for n in col_names if n.endswith("Id") and n not in ("Id", "TenantId")])
        if len(ids) >= 2:
            a, b = ids[0], ids[1]
            purpose = (
                f"Join table that links `{a}` and `{b}` (many-to-many association) for the {mod} domain."
            )
            example = (
                f"Create a row here when associating `{a}` with `{b}` (e.g., granting access or assigning a relationship)."
            )
            return (purpose, example)

    if table.endswith("Policies") or table.endswith("Policy"):
        return (
            f"Policy definitions that control system behavior for {mod} (rules, constraints, and configuration).",
            f"Create/update {table} when you need to change how the system decides or enforces rules in {mod}.",
        )

    if "Quote" in entity or table.endswith("Quotes"):
        return (
            f"Quote records for {mod}. Typically time-bounded and used to present an all-in price/rate/terms before execution.",
            f"Create a {entity} when you need to show the user a price/rate that expires and can be accepted.",
        )

    if "Snapshot" in entity or "Snapshots" in table:
        return (
            f"Precomputed snapshot data for {mod} to make reads/reporting fast without recomputing from raw events.",
            "Write a snapshot during a scheduled job (e.g., nightly) to support dashboards and reporting.",
        )

    if "Transmission" in entity or "Transmissions" in table:
        return (
            "Outbound partner transmission attempts and their lifecycle (status, retries, and errors).",
            "Create a Transmission when sending a request to a partner connector and track until success/failure.",
        )

    if "Challenge" in entity or "Challenges" in table:
        return (
            "Short-lived verification challenges used for proving control of an email/phone/device or approving a sensitive step.",
            "Create a challenge when sending an OTP; verify it when the user submits the code.",
        )

    if "Notification" in entity or "Notifications" in table:
        return (
            "Notification records for messaging users/parties (in-app/email/SMS) with delivery status and payload references.",
            "Create a Notification when an invoice is issued or a payout completes and the user must be informed.",
        )

    if "Webhook" in entity or "Webhook" in table:
        return (
            "Webhook subscription configuration for external systems that want event callbacks.",
            "Create a webhook subscription so a merchant system receives events like `invoice.paid`.",
        )

    if "Case" in entity or "Cases" in table:
        return (
            f"Case records for tracking investigations/holds/reviews in {mod}.",
            "Create a case when an automated check flags something that needs human review.",
        )

    if "Check" in entity or "Checks" in table:
        return (
            f"Check/execution records in {mod} (what was checked, when, and what the result was).",
            "Create a check record when running an automated validation/screening step.",
        )

    if "Run" in entity or "Runs" in table:
        return (
            f"Execution records for {mod} processes (inputs, outputs, timing, and outcome).",
            f"Create a run record whenever a {mod} process is executed so it can be audited and debugged.",
        )

    if "Plan" in entity or "Plans" in table:
        return (
            f"Plan/configuration records in {mod} that define a schedule or strategy applied over time.",
            "Create a plan when you want the system to follow a repeatable schedule (reminders, retries, etc.).",
        )

    if "Rule" in entity or "Rules" in table:
        return (
            f"Rule definitions for {mod} used to drive decisions automatically.",
            f"Create a rule when you want consistent automatic behavior in {mod}.",
        )

    if "Account" in entity or "Accounts" in table:
        return (
            f"Account records in {mod} that represent a durable relationship or capability (not a single transaction).",
            f"Create an account record when onboarding/connecting something that will be reused across many {mod} operations.",
        )

    if "Item" in entity or table.endswith("Items"):
        return (
            f"Item/line records that belong to a parent object in {mod} (breakdown of a larger document or intent).",
            f"Create items when a parent {mod} record needs multiple components.",
        )

    if "Note" in entity or "Notes" in table:
        return (
            f"Free-text notes for {mod} records, used by operations/support without changing structured state.",
            "Add a note when an operator needs to record context for future reviewers.",
        )

    if "Feature" in entity or "Features" in table:
        return (
            "Feature flag state enabling/disabling capabilities per tenant.",
            "Enable a feature for a pilot tenant before broad rollout.",
        )

    if "Setting" in entity or "Settings" in table:
        return (
            "Key/value runtime configuration used to control behavior per tenant (or globally when allowed).",
            "Store a setting like default currency, notification preference, or integration toggle for a tenant.",
        )

    extra_bits: list[str] = []
    if "Status" in col_names:
        extra_bits.append("lifecycle status")
    if any(n.endswith("Json") for n in col_names):
        extra_bits.append("some flexible JSON fields")
    if "IsActive" in col_names:
        extra_bits.append("active/inactive flag")
    if "TenantId" in col_names:
        extra_bits.append("tenant scoping")

    extra = "" if not extra_bits else " Includes " + ", ".join(extra_bits) + "."
    return (
        f"Stores `{entity}` records for the {mod} domain.{extra}",
        f"Create a {entity} when your {mod} workflow needs to persist and later query this kind of data.",
    )


def main() -> int:
    repo_root = pathlib.Path(__file__).resolve().parents[1]
    model_snapshot_cs = (
        repo_root
        / "src"
        / "Aonik.Infrastructure"
        / "Persistence"
        / "Migrations"
        / "AonikDbContextModelSnapshot.cs"
    )
    schema_md = repo_root / "docs" / "database" / "schema.md"

    if not model_snapshot_cs.exists():
        raise SystemExit(f"Missing input: {model_snapshot_cs}")

    snapshot_lines = model_snapshot_cs.read_text(encoding="utf-8", errors="ignore").splitlines()

    entities: list[dict] = []
    current: dict | None = None
    brace = 0
    for line in snapshot_lines:
        m = re.search(r'modelBuilder\.Entity\("([^"]+)"\s*,\s*b\s*=>', line)
        if m:
            current = {"entity": m.group(1), "table": None, "columns": [], "indexesRaw": []}
            brace = 0

        if current is None:
            continue

        brace += line.count("{") - line.count("}")

        mt = re.search(r'\bToTable\("([^"]+)"', line)
        if mt:
            current["table"] = mt.group(1)

        mp = re.search(r'\bProperty\<[^\>]+\>\("([^"]+)"\)', line)
        if mp:
            current["columns"].append({"name": mp.group(1)})

        if ".HasIndex(" in line:
            current["indexesRaw"].append(line.strip())

        if brace <= 0 and "});" in line:
            if current.get("table"):
                # de-dupe columns by name
                names = set()
                cols = []
                for c in current["columns"]:
                    n = c["name"]
                    if n in names:
                        continue
                    names.add(n)
                    cols.append(c)
                current["columns"] = cols
                entities.append(current)
            current = None
    by_table = {e["table"]: e for e in entities}

    missing_purpose = [t for t in sorted(by_table.keys()) if t not in PURPOSE]
    if missing_purpose:
        raise SystemExit("Missing PURPOSE entries for: " + ", ".join(missing_purpose))

    by_module: dict[str, list[str]] = defaultdict(list)
    for t, e in by_table.items():
        by_module[module_for(e["entity"])].append(t)
    for m in list(by_module.keys()):
        by_module[m] = sorted(by_module[m])

    out: list[str] = []
    out.append("# Database Schema (DbContext-derived)")
    out.append("")
    out.append(
        "This document is derived from the EF Core model snapshot, with simple explanations and examples for each table."
    )
    out.append("")
    out.append("- DbContext: `src/Aonik.Infrastructure/Persistence/AonikDbContext.cs`")
    out.append(
        "- Authoritative schema snapshot: `src/Aonik.Infrastructure/Persistence/Migrations/AonikDbContextModelSnapshot.cs`"
    )
    out.append("")
    out.append("## Notes")
    out.append("")
    out.append("- Multi-tenancy: most tables are tenant-scoped via `TenantId` and filtered at query time.")
    out.append("- Auditing/soft delete: many tables include `CreatedAt/By`, `UpdatedAt/By`, and soft-delete fields.")
    out.append("- Orders describe why; payments/payouts describe how; the ledger proves what happened.")
    out.append("")

    for module_name, _ in MODULE_ORDER:
        tables = by_module.get(module_name) or []
        if not tables:
            continue
        out.append(f"## {module_name}")
        out.append("")

        for table in tables:
            e = by_table[table]
            cols = e.get("columns") or []
            purpose, example = auto_purpose(table, e["entity"], cols)
            keys = key_columns(cols)
            idx_cols = parse_indexes(e.get("indexesRaw") or [])

            out.append(f"### `{table}`")
            out.append("")
            out.append(f"- Entity: `{e['entity']}`")
            out.append(f"- Purpose: {purpose}")
            out.append(f"- When to use (example): {example}")
            if keys:
                out.append("- Key columns: " + ", ".join(f"`{c}`" for c in keys))
            if idx_cols:
                rendered = []
                for cols in idx_cols[:4]:
                    rendered.append("(" + ", ".join(f"`{c}`" for c in cols) + ")")
                out.append("- Indexes (partial): " + ", ".join(rendered))
            out.append("")

    schema_md.parent.mkdir(parents=True, exist_ok=True)
    schema_md.write_text("\n".join(out), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
