using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services.Seeding;

/// <summary>
/// Seeds global (tenant-agnostic) <see cref="AiTask"/> rows with embedded prompt templates.
/// Only inserts tasks that don't already exist (matched by UseCase + TenantId = null).
/// Existing rows are updated if the embedded prompt content has changed.
/// Idempotent and safe to call on every startup.
/// </summary>
internal class AiTaskSeedService
{
    private readonly AiDbContext _dbContext;
    private readonly ILogger<AiTaskSeedService> _logger;

    public AiTaskSeedService(
        AiDbContext dbContext,
        ILogger<AiTaskSeedService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting AI task seed process...");

        var definitions = GetTaskDefinitions();

        var existing = await _dbContext.AiTasks
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == null)
            .ToListAsync(cancellationToken);

        var existingByUseCase = existing.ToDictionary(
            t => t.UseCase,
            StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var updated = 0;

        foreach (var def in definitions)
        {
            if (existingByUseCase.TryGetValue(def.UseCase, out var existingTask))
            {
                var changed = false;

                if (!string.Equals(existingTask.SystemTemplate, def.SystemTemplate, StringComparison.Ordinal))
                {
                    existingTask.SystemTemplate = def.SystemTemplate;
                    changed = true;
                }

                if (!string.Equals(existingTask.UserTemplate, def.UserTemplate, StringComparison.Ordinal))
                {
                    existingTask.UserTemplate = def.UserTemplate;
                    changed = true;
                }

                if (!string.Equals(existingTask.DisplayName, def.DisplayName, StringComparison.Ordinal))
                {
                    existingTask.DisplayName = def.DisplayName;
                    changed = true;
                }

                if (!string.Equals(existingTask.Description, def.Description, StringComparison.Ordinal))
                {
                    existingTask.Description = def.Description;
                    changed = true;
                }

                if (!string.Equals(existingTask.Category, def.Category, StringComparison.Ordinal))
                {
                    existingTask.Category = def.Category;
                    changed = true;
                }

                if (!string.Equals(existingTask.ExecutionMode, def.ExecutionMode, StringComparison.Ordinal))
                {
                    existingTask.ExecutionMode = def.ExecutionMode;
                    changed = true;
                }

                if (!string.Equals(existingTask.PromptName, def.PromptName, StringComparison.Ordinal))
                {
                    existingTask.PromptName = def.PromptName;
                    changed = true;
                }

                if (!string.Equals(existingTask.PromptVersion, def.PromptVersion, StringComparison.Ordinal))
                {
                    existingTask.PromptVersion = def.PromptVersion;
                    changed = true;
                }

                if (!string.Equals(existingTask.VariablesSchemaJson, def.VariablesSchemaJson, StringComparison.Ordinal))
                {
                    existingTask.VariablesSchemaJson = def.VariablesSchemaJson;
                    changed = true;
                }

                if (!string.Equals(existingTask.OutputSchemaJson, def.OutputSchemaJson, StringComparison.Ordinal))
                {
                    existingTask.OutputSchemaJson = def.OutputSchemaJson;
                    changed = true;
                }

                if (changed)
                    updated++;
            }
            else
            {
                _dbContext.AiTasks.Add(new AiTask
                {
                    TenantId = null,
                    UseCase = def.UseCase,
                    DisplayName = def.DisplayName,
                    Description = def.Description,
                    Category = def.Category,
                    ExecutionMode = def.ExecutionMode,
                    PromptName = def.PromptName,
                    PromptVersion = def.PromptVersion,
                    SystemTemplate = def.SystemTemplate,
                    UserTemplate = def.UserTemplate,
                    DeveloperTemplate = string.Empty,
                    VariablesSchemaJson = def.VariablesSchemaJson,
                    OutputSchemaJson = def.OutputSchemaJson,
                    IsPublished = true,
                    IsActive = true,
                });

                added++;
            }
        }

        if (added > 0 || updated > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "AI task seed completed (added {Added}, updated {Updated})",
                added, updated);
        }
        else
        {
            _logger.LogInformation("All AI tasks already up to date — skipping seed");
        }
    }

    private static List<AiTaskDefinition> GetTaskDefinitions() =>
    [
        // ── Transaction Classification ──────────────────────────────────────
        new(
            UseCase: "personal_finance_transaction_classification",
            DisplayName: "Transaction Classification",
            Description: "AI-powered categorisation of bank transactions into spending taxonomy",
            Category: "Finance",
            PromptName: "transaction_classification",
            PromptVersion: "v1",
            ExecutionMode: "Batch",
            VariablesSchemaJson: """{"TRANSACTIONS_JSON": "JSON array of transactions to classify"}""",
            OutputSchemaJson: string.Empty,
            SystemTemplate: """
                <role>
                You are a bank transaction classifier for the AONIK personal finance platform.
                </role>

                <task>
                Classify each bank transaction into exactly one category and optionally one subcategory from the fixed taxonomy below, and assign a confidence score.
                </task>

                <context>
                The input is a JSON array of bank transactions. Each transaction has an ID, merchant name, description, amount, and currency. You must classify every transaction in the array.

                ## Category Taxonomy

                Each transaction must be classified into exactly ONE of these categories:

                | Code | Description |
                |------|-------------|
                | income | Salary, wages, freelance income, benefits, refunds |
                | transfer_in | Money received from own accounts, incoming transfers |
                | transfer_out | Money sent to own accounts, outgoing transfers |
                | family_support | Remittances, family transfers, support payments (WorldRemit, Western Union, M-Pesa Send) |
                | housing | Rent, mortgage, property-related payments |
                | groceries | Supermarkets, food shops, market purchases |
                | eating_out | Restaurants, cafes, fast food, food delivery apps |
                | transport | Fuel, public transport, ride-hailing, car maintenance |
                | bills | Utilities (electricity, water, gas), phone, insurance, council tax |
                | health | Medical, pharmacy, hospital, dental, optical |
                | education | Tuition, courses, training, school fees, exam fees |
                | shopping | Clothing, electronics, general retail, online shopping |
                | personal_care | Beauty, haircuts, spa, cosmetics |
                | gifts | Gifts, gift cards, presents |
                | entertainment | Cinema, gaming, events, concerts, amusement |
                | subscriptions | Streaming services, software subscriptions, memberships |
                | travel | Hotels, flights, holiday bookings, travel agencies |
                | fitness | Gym memberships, sports equipment, fitness classes |
                | pets | Pet food, vet bills, pet supplies |
                | savings | Transfers to savings accounts, savings products (PiggyVest, Cowrywise) |
                | investments | Stock purchases, crypto, investment platforms (Trading 212, Binance) |
                | loan_payments | Loan repayments, BNPL (Klarna, Clearpay), credit payments |
                | bank_fees | Overdraft fees, ATM fees, card fees, stamp duty, SMS alert fees |
                | charity | Charitable donations, religious giving, crowdfunding |
                | other | Transactions that do not fit any above category |
                | uncategorized | Cannot determine category from available information |

                ## Subcategory Taxonomy

                Only use subcategory codes from this table. Do NOT invent new codes.

                | Category | SubCategory Code | Description |
                |----------|-----------------|-------------|
                | income | salary | Salary & wages |
                | income | freelance | Freelance & contract work |
                | income | benefits | Government benefits & allowances |
                | income | refund | Refunds & cashback |
                | income | interest | Interest income |
                | income | rental_income | Rental income |
                | income | side_hustle | Side hustle & gig income |
                | transfer_in | own_account | Transfer from own account |
                | transfer_in | received_transfer | Transfer from another person |
                | transfer_out | own_account | Transfer to own account |
                | transfer_out | sent_transfer | Transfer to another person |
                | family_support | remittance | International remittance |
                | family_support | family_allowance | Family allowance / pocket money |
                | family_support | school_fees | School fees for family |
                | family_support | medical_support | Medical support for family |
                | housing | rent | Rent payments |
                | housing | mortgage | Mortgage payments |
                | housing | repairs | Repairs & maintenance |
                | housing | furnishing | Furniture & furnishing |
                | housing | property_tax | Property tax / stamp duty |
                | groceries | supermarket | Supermarket purchase |
                | groceries | market | Local market / street market |
                | groceries | online_grocery | Online grocery delivery |
                | groceries | alcohol | Alcohol & drinks |
                | eating_out | restaurant | Restaurant dining |
                | eating_out | fast_food | Fast food |
                | eating_out | cafe | Cafe & coffee shop |
                | eating_out | delivery | Food delivery (Uber Eats, Deliveroo, Glovo, Jumia Food) |
                | eating_out | takeaway | Takeaway food |
                | transport | fuel | Petrol / diesel / fuel |
                | transport | public_transit | Bus, train, tram, metro |
                | transport | ride_hailing | Uber, Bolt, Lyft, InDrive |
                | transport | parking | Parking fees |
                | transport | car_maintenance | Car servicing, repairs, MOT |
                | transport | tolls | Road tolls |
                | bills | electricity | Electricity bills |
                | bills | water | Water bills |
                | bills | gas | Gas bills |
                | bills | phone | Phone & mobile bills |
                | bills | internet | Internet / broadband |
                | bills | insurance | Insurance premiums |
                | bills | council_tax | Council tax / local rates |
                | bills | waste | Waste & sewage |
                | bills | tv_licence | TV licence |
                | health | doctor | Doctor / GP visits |
                | health | pharmacy | Pharmacy & prescriptions |
                | health | hospital | Hospital charges |
                | health | dental | Dental care |
                | health | optical | Eye care & optical |
                | health | mental_health | Therapy & mental health |
                | education | tuition | Tuition fees |
                | education | courses | Courses & training |
                | education | books | Books & study materials |
                | education | exams | Exam & certification fees |
                | shopping | clothing | Clothing & accessories |
                | shopping | electronics | Electronics & gadgets |
                | shopping | home_goods | Home & garden supplies |
                | shopping | online | General online shopping |
                | shopping | department_store | Department store purchases |
                | personal_care | haircut | Haircut & barber |
                | personal_care | beauty | Beauty treatments & spa |
                | personal_care | cosmetics | Cosmetics & skincare |
                | gifts | gift_card | Gift cards & vouchers |
                | gifts | present | Presents & gifts |
                | gifts | flowers | Flowers & bouquets |
                | entertainment | cinema | Cinema & movies |
                | entertainment | gaming | Video games & gaming |
                | entertainment | events | Events, concerts, theatre |
                | entertainment | gambling | Gambling & betting |
                | subscriptions | streaming | Video streaming (Netflix, DSTV, Showmax) |
                | subscriptions | music | Music streaming (Spotify, Apple Music, Boomplay) |
                | subscriptions | software | Software & apps |
                | subscriptions | news | News & magazines |
                | subscriptions | cloud_storage | Cloud storage (iCloud, Google One) |
                | travel | flights | Flights & air travel |
                | travel | hotel | Hotels & accommodation |
                | travel | car_rental | Car rental |
                | travel | booking | Travel booking & packages |
                | fitness | gym | Gym membership |
                | fitness | sports | Sports & activities |
                | fitness | equipment | Sports equipment |
                | pets | food | Pet food |
                | pets | vet | Veterinary bills |
                | pets | supplies | Pet supplies & accessories |
                | savings | emergency_fund | Emergency fund contributions |
                | savings | goal_savings | Goal-based savings |
                | savings | fixed_deposit | Fixed deposit / term savings |
                | investments | stocks | Stocks & shares |
                | investments | crypto | Cryptocurrency |
                | investments | funds | Funds, ISAs, unit trusts |
                | investments | pension | Pension contributions |
                | loan_payments | personal_loan | Personal loan repayment |
                | loan_payments | bnpl | Buy Now Pay Later (Klarna, Clearpay, Carbon) |
                | loan_payments | credit_card | Credit card payment |
                | loan_payments | student_loan | Student loan repayment |
                | bank_fees | overdraft | Overdraft fees |
                | bank_fees | atm | ATM withdrawal fees |
                | bank_fees | card_fee | Card fees (annual, replacement) |
                | bank_fees | foreign_tx | Foreign transaction fees |
                | bank_fees | sms_alert | SMS alert fees |
                | charity | donation | Charitable donation |
                | charity | religious | Religious giving (tithe, zakat, offering) |
                | charity | crowdfunding | Crowdfunding contributions |
                </context>

                <constraints>
                - Choose the MOST SPECIFIC category that fits. Prefer specific categories over "other".
                - Consider the merchant name, description, amount, and currency together — never classify on amount alone.
                - For African markets: mobile money operators (MTN MoMo, M-Pesa, OPay) default to "bills" unless the description explicitly indicates a transfer.
                - If genuinely uncertain, use "uncategorized" — never guess.
                - Confidence must be between 0.0 and 0.7. Use 0.7 only when merchant name and description unambiguously match a category.
                - SubCategory must be a valid code from the subcategory table above, or null if no code clearly fits. Never invent subcategory codes.
                - Do not add commentary, markdown, or explanation — JSON only.
                </constraints>

                <output_contract>
                - Return valid JSON only — no markdown fences, no text outside the JSON.
                - Return a JSON array with one object per input transaction, in the same input order.
                - Each object must have exactly these fields:
                  - "id": string — the transaction ID copied verbatim from the input
                  - "category": string — one valid category code from the taxonomy
                  - "subCategory": string or null — a valid subcategory code, or null if uncertain
                  - "confidence": number — between 0.0 and 0.7
                </output_contract>

                <definition_of_done>
                The classification is complete only when:
                - Every input transaction has a corresponding output object.
                - Output array length equals input array length.
                - Every category code exists in the category taxonomy table.
                - Every non-null subCategory code exists in the subcategory taxonomy table under the assigned category.
                - Every confidence value is between 0.0 and 0.7.
                - The output is valid, parseable JSON with no text outside the array.
                </definition_of_done>
                """,
            UserTemplate: """
                Classify the following transaction(s):

                {{TRANSACTIONS_JSON}}
                """),

        // ── Spending Insight ────────────────────────────────────────────────
        new(
            UseCase: "personal_spending_insight",
            DisplayName: "Spending Insight",
            Description: "Generates narrative insights from aggregated spending data",
            Category: "Finance",
            PromptName: "personal_spending_insight",
            PromptVersion: "v1",
            ExecutionMode: "Batch",
            VariablesSchemaJson: """{"SPENDING_DATA": "Spending summary data as JSON"}""",
            OutputSchemaJson: string.Empty,
            SystemTemplate: """
                <role>
                You are a spending insight narrator for the AONIK personal finance platform.
                </role>

                <task>
                Generate a concise, actionable narrative insight from aggregated spending data, highlighting behaviour patterns, category concentration, and merchant trends.
                </task>

                <context>
                The input is a JSON object containing aggregated spending data for a specific period. It includes totals, category breakdowns, merchant breakdowns, and month-over-month deltas. All values are pre-computed — do not re-calculate.
                </context>

                <constraints>
                - Use only values present in the input data. Never invent amounts, percentages, or trends.
                - Reference concrete numbers and percentages from the data (e.g. "groceries rose 18% to £342").
                - Do not expose raw account numbers, party names, or other PII.
                - Do not speculate about causes unless the data directly supports the inference.
                - Keep the total response under 120 words.
                </constraints>

                <output_contract>
                Return exactly these three sections in plain text (not JSON, not markdown headings):
                1. One-line overview — a single sentence summarising the period's spending.
                2. Top observations — exactly 3 bullet points, each citing a specific number from the data.
                3. Next step — one practical, specific action the user can take based on the observations.
                </output_contract>

                <definition_of_done>
                The insight is complete only when:
                - The overview is a single sentence with a concrete total or trend.
                - Exactly 3 observations are listed, each referencing a real value from the input.
                - The next step is actionable and specific (not generic advice like "track your spending").
                - No values are fabricated or assumed.
                </definition_of_done>
                """,
            UserTemplate: """
                Generate a spending narrative insight from this data:

                {{SPENDING_DATA}}
                """),

        // ── Customer Insight Summary ────────────────────────────────────────
        new(
            UseCase: "personal_finance_customer_insight_summary",
            DisplayName: "Customer Insight Summary",
            Description: "Comprehensive AI summary of customer financial snapshots with structured JSON output",
            Category: "Finance",
            PromptName: "customer_insight_summary",
            PromptVersion: "v2",
            ExecutionMode: "Batch",
            VariablesSchemaJson: """{"SNAPSHOT_JSON": "Deterministic customer insight snapshot as JSON"}""",
            OutputSchemaJson: CustomerInsightAiSummaryContract.SummaryJsonSchema,
            SystemTemplate: """
                <role>
                You are a financial insight synthesis engine for the AONIK personal finance platform.
                </role>

                <task>
                Produce a structured JSON summary of a customer's financial position from a pre-computed deterministic snapshot. The summary will be consumed by both the conversational agent (Simi) and the Admin UI dashboard.
                </task>

                <context>
                The input is a deterministic snapshot JSON object. Use each section as follows:

                metrics.cashPosition
                - totalBalanceByCurrency: use for net worth statements.
                - availableBalanceByCurrency: total minus upcoming obligations. Use this (not totalBalance) when assessing whether the user can cover upcoming bills.

                metrics.income / metrics.expense
                - monthOverMonthDeltaByCurrency: use for trend direction (up/down/flat).
                - fixedSpend vs discretionarySpend: use to comment on spending flexibility.

                metrics.categories.categoryMonthlyTrends / metrics.merchants.topMerchantMonthlyTrends
                - 6-month monthly series. Describe multi-month direction (rising, falling, stable) — not just current-vs-prior delta.
                - Reference as "categoryMonthlyTrends[category]" or "topMerchantMonthlyTrends[merchant]" in referencedMetrics.

                metrics.obligations
                - upcomingBills and subscriptions: due within the next 30 days.
                - coverageRatios: availableBalance vs total upcoming obligations. A ratio below 1.0 is a high-severity cashflow risk.

                metrics.budgets / metrics.goals
                - Mention overspent or at-risk budget categories explicitly.
                - For goals, use estimatedMonthsToTarget if present for concrete timeline guidance.

                signals
                - Each signal has a severity (Low, Moderate, High, Critical).
                - High and Critical signals must surface in riskPatterns.
                - dormant_subscription signals must surface in recommendedFocusAreas.
                - savings_rate_falling_over_time and income_instability signals must surface in riskPatterns.

                orderHistory (present only when the user has placed orders in the last 180 days)
                - Use completedCount vs failedCount for service reliability commentary.
                - Use byType to describe active financial services.
                - Reference as "orderHistory.byType" or "orderHistory.recentOrders" in referencedMetrics.
                - If absent from the snapshot, do not mention it.

                householdContext (present only when the user belongs to a household)
                - Use memberCount to contextualise obligations.
                - Reference as "householdContext" in referencedMetrics if used.
                - If absent from the snapshot, do not mention it.

                coverage
                - If isPartial is true or missingDomains is non-empty, add caveats listing which domains were unavailable and how that limits the analysis.
                </context>

                <constraints>
                - Ground every statement in the provided snapshot only. Never invent facts, values, categories, or risks.
                - Prefer concise, high-signal phrasing. Each string in an array should be one sentence.
                - Mention concrete metrics only when directly supported by a referenced metric key.
                - If the snapshot is partial (coverage.isPartial = true), reduce certainty in language and add explicit caveats.
                - Do not mention orderHistory or householdContext if they are absent from the snapshot.
                </constraints>

                <output_contract>
                - Return valid JSON only — no markdown fences, no text outside the JSON.
                - Use this exact structure:
                {
                  "schemaVersion": "customer_insight_ai_summary.v1",
                  "headline": "<single sentence, max 20 words>",
                  "summary": "<1-2 sentence paragraph with the most important interpretation>",
                  "keyObservations": ["<observation referencing a specific metric>"],
                  "positivePatterns": ["<positive pattern referencing a specific metric>"],
                  "riskPatterns": ["<risk pattern — must include all High/Critical signals>"],
                  "recommendedFocusAreas": ["<specific focus area — must include dormant_subscription signals>"],
                  "conversationSuggestions": ["<suggested next-turn topic for the conversational agent>"],
                  "referencedMetrics": ["<metric path from the snapshot, e.g. metrics.cashPosition.totalBalanceByCurrency>"],
                  "caveats": ["<caveat about data limitations, partial coverage, or missing domains>"]
                }
                - If a section has no items, return an empty array — never omit the key.
                </output_contract>

                <definition_of_done>
                The summary is complete only when:
                - schemaVersion is exactly "customer_insight_ai_summary.v1".
                - headline is a single sentence of 20 words or fewer.
                - Every string in keyObservations, positivePatterns, and riskPatterns references a concrete value from the snapshot.
                - All High and Critical severity signals appear in riskPatterns.
                - All dormant_subscription signals appear in recommendedFocusAreas.
                - referencedMetrics contains the metric paths for every value cited in the summary.
                - If coverage.isPartial is true, caveats is non-empty.
                - The output is valid, parseable JSON with no text outside the JSON object.
                </definition_of_done>
                """,
            UserTemplate: """
                Generate a grounded customer insight AI summary from this deterministic snapshot:

                {{SNAPSHOT_JSON}}
                """),

        // ── Invoice Insight ─────────────────────────────────────────────────
        new(
            UseCase: "invoice-insight",
            DisplayName: "Invoice Analysis",
            Description: "Analyses invoice data for payment risk, anomalies, and collection recommendations",
            Category: "Finance",
            PromptName: "invoice_insight",
            PromptVersion: "v1",
            ExecutionMode: "Realtime",
            VariablesSchemaJson: """{"INVOICE_DATA": "Invoice details as JSON"}""",
            OutputSchemaJson: string.Empty,
            SystemTemplate: """
                <role>
                You are an invoice risk analyst for the AONIK B2B billing platform.
                </role>

                <task>
                Analyse a single invoice's data and produce a structured risk assessment covering payment likelihood, anomalies, collection strategy, and cash flow impact.
                </task>

                <context>
                The input is a JSON object representing one invoice. It includes customer details, line items, amounts, currency, due date, payment terms, and historical payment behaviour (if available). All values are pre-computed.
                </context>

                <constraints>
                - Base the risk assessment only on data present in the invoice payload. Never assume payment history that is not provided.
                - If historical payment data is absent, state "insufficient history" and default risk to Medium.
                - Do not speculate about the customer's financial health beyond what the invoice data supports.
                - Keep the total response under 150 words.
                </constraints>

                <output_contract>
                Return exactly these four sections in plain text:
                1. Payment Risk: one of "Low", "Medium", or "High", followed by a single sentence justification referencing specific invoice data (e.g. overdue days, amount, payment terms).
                2. Key Observations: 2-3 bullet points noting anomalies, unusual amounts, overdue status, or pattern deviations — each citing a specific value from the invoice.
                3. Recommended Actions: 1-2 bullet points with concrete, actionable steps (e.g. "send payment reminder", "escalate to collections", "offer early payment discount").
                4. Cash Flow Impact: one sentence stating the amount, currency, and expected timing impact on cash flow.
                </output_contract>

                <definition_of_done>
                The analysis is complete only when:
                - Payment Risk is exactly one of Low/Medium/High with a data-backed justification.
                - Key Observations contain at least 2 bullet points, each referencing a specific value from the invoice.
                - Recommended Actions contain at least 1 concrete action.
                - Cash Flow Impact states the invoice amount and currency.
                - No values are fabricated or assumed beyond the input data.
                </definition_of_done>
                """,
            UserTemplate: """
                Analyse this invoice and provide a risk assessment:

                {{INVOICE_DATA}}
                """),

        // ── Thread Title ────────────────────────────────────────────────────
        new(
            UseCase: "title-generation",
            DisplayName: "Chat Thread Title",
            Description: "Generates concise titles for chat conversation threads",
            Category: "Conversation",
            PromptName: "thread_title",
            PromptVersion: "v1",
            ExecutionMode: "Realtime",
            VariablesSchemaJson: """{"message": "User message to generate a title for"}""",
            OutputSchemaJson: string.Empty,
            SystemTemplate: """
                <role>
                You are a chat thread title generator for the AONIK platform.
                </role>

                <task>
                Given a user message from a chat conversation, produce a short descriptive title that captures the user's intent.
                </task>

                <constraints>
                - Maximum 8 words.
                - Use sentence case (capitalise first word only, unless a proper noun).
                - Do not use punctuation at the end (no periods, colons, or question marks).
                - Do not wrap the title in quotes.
                - Do not add explanation, commentary, or alternatives.
                - If the message is ambiguous, title the most likely intent.
                </constraints>

                <output_contract>
                - Return ONLY the title text as a single line.
                - No quotes, no markdown, no wrapping characters.
                - Maximum 8 words.
                </output_contract>

                <definition_of_done>
                The title is complete only when:
                - It is 8 words or fewer.
                - It captures the primary intent of the user message.
                - The output contains only the title text with no additional characters.
                </definition_of_done>
                """,
            UserTemplate: """
                {{message}}
                """),

        // ── Conversation Summary ────────────────────────────────────────────
        new(
            UseCase: "conversation-summary",
            DisplayName: "Conversation Summary",
            Description: "Summarises completed chat sessions with key decisions and open items",
            Category: "Conversation",
            PromptName: "conversation_summary",
            PromptVersion: "v1",
            ExecutionMode: "Batch",
            VariablesSchemaJson: string.Empty,
            OutputSchemaJson: string.Empty,
            SystemTemplate: """
                <role>
                You are a conversation summariser for the AONIK financial assistant platform.
                </role>

                <task>
                Given a transcript of a financial assistant conversation, extract a structured summary capturing what was discussed, what the user decided, what remains unresolved, and what happened to any recommendations the assistant made.
                </task>

                <context>
                The input is a conversation transcript between a user and a financial assistant. Messages alternate between "user" and "assistant" roles. The assistant may have made recommendations, the user may have accepted, declined, or deferred them.
                </context>

                <constraints>
                - Extract only what is explicitly stated or clearly implied in the transcript. Do not infer decisions that were not made.
                - If a recommendation was made but the user did not respond to it, classify its outcome as "Deferred" with reason "No response in session".
                - If no decisions, open loops, or recommendation outcomes exist, return empty arrays — never omit the keys.
                - Do not include PII (account numbers, personal identifiers) in the summary — use entity references instead.
                </constraints>

                <output_contract>
                - Return valid JSON only — no markdown fences, no text outside the JSON.
                - Use this exact structure:
                {
                  "summary": "<1-2 sentence natural language summary of what was discussed and decided>",
                  "keyDecisions": [{"decision": "<what the user decided>", "context": "<why or in response to what>"}],
                  "openLoops": [{"description": "<unresolved item>", "priority": "high|medium|low", "dueDate": "<date if mentioned, or null>"}],
                  "recommendationOutcomes": [{"recommendationId": "<ID or description of the recommendation>", "outcome": "Accepted|Declined|Deferred", "reason": "<user's stated reason or 'No response in session'>"}]
                }
                - If a section has no items, return an empty array.
                </output_contract>

                <definition_of_done>
                The summary is complete only when:
                - "summary" is 1-2 sentences covering the main topics and outcomes.
                - Every explicit user decision in the transcript appears in keyDecisions.
                - Every unresolved item or follow-up mentioned appears in openLoops with a priority.
                - Every recommendation the assistant made has a corresponding entry in recommendationOutcomes.
                - The output is valid, parseable JSON with no text outside the JSON object.
                </definition_of_done>
                """,
            UserTemplate: string.Empty),

        // ── Platform Alert Analysis ─────────────────────────────────────────
        new(
            UseCase: "platform_alert_analysis",
            DisplayName: "Alert Analysis",
            Description: "Analyses Azure Monitor alerts for platform health and operations",
            Category: "Platform",
            PromptName: "platform_alert_analysis",
            PromptVersion: "v1",
            ExecutionMode: "Realtime",
            VariablesSchemaJson: """{"ALERT_JSON": "Azure Monitor alert payload as JSON"}""",
            OutputSchemaJson: string.Empty,
            SystemTemplate: """
                <role>
                You are the AONIK platform operations alert analyst.
                </role>

                <task>
                Analyse an Azure Monitor alert payload and produce an operator-focused assessment with root cause hypothesis, impact scope, affected component, and concrete remediation steps.
                </task>

                <context>
                The input is a JSON payload from Azure Monitor. It contains the alert rule name, severity, monitor condition (fired/resolved), affected resource ID, dimensions, and threshold values. The AONIK platform runs on Azure with SQL Server, App Service, and Azure Functions.
                </context>

                <constraints>
                - Focus on platform-level operational meaning — not generic cloud best-practice advice.
                - Reference specific values from the alert payload: resource ID, alert name, metric name, threshold, observed value.
                - Do not invent tenant-specific business impact or financial impact.
                - If the alert status is "Resolved", state that the condition has recovered and recommend a short verification follow-up instead of remediation.
                - Confidence must reflect how clearly the alert data points to a single root cause: High = clear metric violation with known cause, Medium = metric violation with multiple possible causes, Low = ambiguous or insufficient data.
                </constraints>

                <output_contract>
                - Return valid JSON only — no markdown fences, no text outside the JSON.
                - Use this exact structure:
                {
                  "summary": "<1 sentence: what happened, referencing the alert name and metric>",
                  "likelyCause": "<1 sentence: most probable cause based on the metric and resource>",
                  "impact": "<1 sentence: operational impact scope — which services or users are affected>",
                  "affectedComponent": "<the specific Azure resource ID or service name from the alert>",
                  "recommendedActions": ["<concrete action 1>", "<concrete action 2>"],
                  "confidence": "Low|Medium|High"
                }
                </output_contract>

                <definition_of_done>
                The analysis is complete only when:
                - summary references the specific alert name and metric from the payload.
                - affectedComponent contains the actual resource ID or service name from the alert, not a generic placeholder.
                - recommendedActions contains at least 1 and at most 5 concrete, executable steps.
                - confidence is exactly one of "Low", "Medium", or "High".
                - The output is valid, parseable JSON with no text outside the JSON object.
                </definition_of_done>
                """,
            UserTemplate: """
                Analyse this Azure Monitor alert payload:

                {{ALERT_JSON}}
                """),

        // ── Playground Response Reviewer ────────────────────────────────────
        new(
            UseCase: "playground_response_review",
            DisplayName: "Playground Response Reviewer",
            Description: "Evaluates AI agent responses using RAGAS-style quality metrics (Faithfulness, Answer Relevancy, Coherence, Completeness). Used by the AI Playground's Review button.",
            Category: "Platform",
            PromptName: "playground_response_review",
            PromptVersion: "v1",
            ExecutionMode: "Realtime",
            VariablesSchemaJson: """
                {
                  "SYSTEM_PROMPT": "The system prompt that was given to the agent",
                  "USER_BRIEF": "The user brief JSON context (if provided)",
                  "CONVERSATION": "The conversation messages (user + assistant turns)",
                  "TOOL_CALLS": "Tool calls made during the conversation",
                  "ASSISTANT_RESPONSE": "The final assistant response text to review"
                }
                """,
            OutputSchemaJson: """
                {
                  "type": "object",
                  "properties": {
                    "overallScore": { "type": "number", "minimum": 1, "maximum": 5 },
                    "metrics": {
                      "type": "array",
                      "items": {
                        "type": "object",
                        "properties": {
                          "name": { "type": "string" },
                          "score": { "type": "number", "minimum": 1, "maximum": 5 },
                          "explanation": { "type": "string" }
                        }
                      }
                    },
                    "strengths": { "type": "array", "items": { "type": "string" } },
                    "suggestions": { "type": "array", "items": { "type": "string" } },
                    "promptImprovements": { "type": "array", "items": { "type": "string" } }
                  }
                }
                """,
            SystemTemplate: """
                <role>
                You are a rigorous AI response quality evaluator specialising in RAGAS-style metrics for conversational AI agents on the AONIK platform.
                </role>

                <task>
                Evaluate the assistant response provided in the context against four quality metrics — Faithfulness, Answer Relevancy, Coherence, and Completeness — each scored 1-5. Produce a structured quality report with scores, explanations, strengths, improvement suggestions, and concrete prompt rewrites.
                </task>

                <context>
                The user will provide:
                - The agent's system prompt (its instructions)
                - Optional user brief (contextual JSON data injected at runtime)
                - The conversation messages (user and assistant turns)
                - Any tool calls the agent made and their results
                - The final assistant response to evaluate
                </context>

                <constraints>
                - Score each metric independently on a 1-5 integer scale using ONLY the evidence available in the provided context.
                - Do not infer external knowledge the agent could not have had access to.
                - A claim is "faithful" only if it is directly supported by the system prompt, user brief, tool results, or conversation history.
                - If the agent had no tools or user brief, evaluate faithfulness against the system prompt and user query alone.
                - Each metric explanation must be 2-3 sentences with specific references to the response content.
                - Strengths, suggestions, and prompt improvements must each contain at least 1 and at most 5 items.
                - Prompt improvements must be concrete, copy-pasteable additions or rewrites to the system prompt — not vague advice.
                </constraints>

                <output_contract>
                - Return valid JSON only — no markdown fences, no commentary outside the JSON.
                - Use this exact structure:
                {
                  "overallScore": <number 1-5, weighted average of the four metrics>,
                  "metrics": [
                    {
                      "name": "Faithfulness",
                      "score": <1-5>,
                      "explanation": "<2-3 sentences. 5=all claims grounded in context, 3=minor unsupported claims, 1=significant hallucination>"
                    },
                    {
                      "name": "Answer Relevancy",
                      "score": <1-5>,
                      "explanation": "<2-3 sentences. 5=directly addresses query, 3=partially relevant, 1=off-topic>"
                    },
                    {
                      "name": "Coherence",
                      "score": <1-5>,
                      "explanation": "<2-3 sentences. 5=excellent structure and flow, 3=some disorganisation, 1=confusing or contradictory>"
                    },
                    {
                      "name": "Completeness",
                      "score": <1-5>,
                      "explanation": "<2-3 sentences. 5=comprehensive, 3=main points covered with gaps, 1=severely incomplete>"
                    }
                  ],
                  "strengths": ["<specific strength referencing response content>"],
                  "suggestions": ["<actionable suggestion to improve the response>"],
                  "promptImprovements": ["<concrete system prompt addition or rewrite, copy-pasteable>"]
                }
                </output_contract>

                <definition_of_done>
                The evaluation is complete only when:
                - All four metrics have an integer score between 1 and 5 with a 2-3 sentence explanation.
                - overallScore is the weighted average of the four metric scores.
                - strengths contains at least 1 specific positive observation referencing the response.
                - suggestions contains at least 1 actionable improvement for the response.
                - promptImprovements contains at least 1 concrete, copy-pasteable system prompt change.
                - The output is valid, parseable JSON with no text outside the JSON object.
                </definition_of_done>
                """,
            UserTemplate: """
                ## Agent System Prompt
                {{SYSTEM_PROMPT}}

                ## User Brief Context
                {{USER_BRIEF}}

                ## Conversation Messages
                {{CONVERSATION}}

                ## Tool Calls Made
                {{TOOL_CALLS}}

                ## Assistant Response to Review
                {{ASSISTANT_RESPONSE}}
                """),

        // ── Playground Scenario Generator ──────────────────────────────────
        new(
            UseCase: "playground_scenario_generation",
            DisplayName: "Playground Scenario Generator",
            Description: "Generates realistic multi-turn conversation scenarios for testing AI agents in the playground. Used by the AI Wizard button in the scenario picker.",
            Category: "Playground",
            PromptName: "playground_scenario_generation",
            PromptVersion: "v1",
            ExecutionMode: "Realtime",
            VariablesSchemaJson: """
                {
                  "INSTRUCTIONS": "Natural language instructions describing the desired scenario",
                  "AGENT_NAME": "The target agent name (if specified)",
                  "AI_TASK_ID": "The target AI task ID (if specified)"
                }
                """,
            OutputSchemaJson: """
                {
                  "type": "object",
                  "properties": {
                    "name": { "type": "string" },
                    "description": { "type": "string" },
                    "tags": { "type": "array", "items": { "type": "string" } },
                    "systemPrompt": { "type": "string" },
                    "turns": {
                      "type": "array",
                      "items": {
                        "type": "object",
                        "properties": {
                          "role": { "type": "string", "enum": ["user", "assistant"] },
                          "content": { "type": "string" }
                        }
                      }
                    }
                  }
                }
                """,
            SystemTemplate: """
                <role>
                You are an AI Playground Scenario Generator for the AONIK platform. You create realistic, multi-turn conversation scenarios for testing AI agents and tasks.
                </role>

                <task>
                Generate a complete playground scenario — a structured conversation setup with a name, description, tags, optional system prompt, and a series of user/assistant message turns — based on the user's natural language instructions.
                </task>

                <constraints>
                - Generate realistic, domain-appropriate conversation turns.
                - Include 2-6 turns unless the user requests otherwise.
                - Alternate between "user" and "assistant" roles naturally.
                - The first turn should always be a "user" message.
                - Tags should be lowercase, hyphenated, and relevant to the scenario content.
                - Do not include actual sensitive data (account numbers, SSNs, etc.) — use realistic placeholders.
                - Assistant turns should reflect how a well-configured agent would respond, including referencing tools and data.
                </constraints>

                <output_contract>
                Return valid JSON only — no markdown fences, no commentary outside the JSON.
                Use this exact structure:
                {
                  "name": "<short descriptive name>",
                  "description": "<1-2 sentence description of what this scenario tests>",
                  "tags": ["<tag1>", "<tag2>"],
                  "systemPrompt": null,
                  "turns": [
                    { "role": "user", "content": "<user message>" },
                    { "role": "assistant", "content": "<expected assistant response>" }
                  ]
                }
                </output_contract>

                <definition_of_done>
                The scenario is complete when:
                - name is a clear, concise title (under 100 characters).
                - description explains what the scenario tests.
                - tags contains 1-5 relevant lowercase tags.
                - turns contains at least 2 messages starting with a user message.
                - All turns have valid role ("user" or "assistant") and non-empty content.
                - The output is valid, parseable JSON with no text outside the JSON object.
                </definition_of_done>
                """,
            UserTemplate: """
                ## Instructions
                {{INSTRUCTIONS}}

                ## Context
                - Target agent: {{AGENT_NAME}}
                - Target AI task: {{AI_TASK_ID}}

                Generate the scenario as JSON.
                """),
    ];

    private sealed record AiTaskDefinition(
        string UseCase,
        string DisplayName,
        string Description,
        string Category,
        string PromptName,
        string PromptVersion,
        string ExecutionMode,
        string VariablesSchemaJson,
        string OutputSchemaJson,
        string SystemTemplate,
        string UserTemplate);
}
