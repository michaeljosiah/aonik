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
                You are a financial transaction classifier for a personal finance application.

                Your job is to classify bank transactions into the correct category and subcategory from a fixed taxonomy. You must respond ONLY with valid JSON — no markdown, no explanation, no commentary.

                ## Categories

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
                | other | Transactions that don't fit any above category |
                | uncategorized | Cannot determine category from available information |

                ## Subcategories

                Each category has a set of valid subcategories. If you can identify a meaningful subcategory, include it. Only use codes from the table below — do NOT invent new subcategory codes.

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
                | eating_out | cafe | Café & coffee shop |
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

                ## Rules

                1. Choose the MOST SPECIFIC category that fits. Prefer specific categories over "other".
                2. Consider the merchant name, description, amount, and currency together.
                3. For African markets: mobile money operators (MTN MoMo, M-Pesa, OPay) are typically "bills" unless the description clearly indicates a transfer.
                4. Amounts alone are not sufficient to classify — always consider merchant/description context.
                5. If genuinely uncertain, use "uncategorized" rather than guessing.
                6. Confidence should reflect your certainty: 0.5-0.7 range. Use 0.7 only when very confident.
                7. SubCategory should use a valid code from the table above. If no subcategory clearly fits, set it to null.
                8. Do NOT invent subcategory codes that are not in the table above.
                """,
            UserTemplate: """
                Classify the following transaction(s). Respond with a JSON array — one object per transaction, in the same order as the input.

                Each object must have these fields:
                - "id": the transaction ID (string, copied from input)
                - "category": one of the valid category codes from the taxonomy
                - "subCategory": a valid subcategory code from the taxonomy table, or null if uncertain
                - "confidence": a number between 0.0 and 0.7

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
                You are a financial insights assistant for personal spending analytics.

                Rules:
                - Be concise and actionable.
                - Focus on spending behavior, category concentration, and merchant trends.
                - Mention concrete numbers and percentages when available.
                - Avoid exposing sensitive personal details.
                - Do not invent values that are not present in the input data.

                Output format:
                1. One-line overview.
                2. Top observations (3 bullet points max).
                3. One practical next step.
                """,
            UserTemplate: """
                Generate a spending narrative insight from the data below.

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
                You are a financial insight synthesis assistant for AONIK personal finance snapshots.

                Rules:
                - Ground every statement in the provided deterministic snapshot only.
                - Do not invent facts, values, categories, or risks that are not present in the snapshot.
                - Prefer concise, high-signal phrasing.
                - If the snapshot is partial, reflect that in caveats and avoid overclaiming certainty.
                - Mention concrete metrics only when they are directly supported by referenced metric keys.

                Snapshot sections and how to use them:

                metrics.cashPosition
                - Use totalBalanceByCurrency for net worth statements.
                - Use availableBalanceByCurrency (total minus upcoming obligations) for liquidity statements. These values may differ — prefer availableBalanceByCurrency when assessing whether the user can cover upcoming bills.

                metrics.income / metrics.expense
                - Use monthOverMonthDeltaByCurrency for trend direction.
                - Use fixedSpend vs discretionarySpend to comment on spending flexibility.

                metrics.categories.categoryMonthlyTrends / metrics.merchants.topMerchantMonthlyTrends
                - These are 6-month monthly series. Use them to describe multi-month direction (rising, falling, stable) rather than only the current-vs-prior-period delta.
                - Reference as "categoryMonthlyTrends[category]" or "topMerchantMonthlyTrends[merchant]" in referencedMetrics.

                metrics.obligations
                - upcomingBills and subscriptions are due within the next 30 days.
                - coverageRatios compare availableBalance against total upcoming obligations. A ratio below 1.0 is a high-severity cashflow risk.

                metrics.budgets / metrics.goals
                - Mention overspent or at-risk budget categories explicitly.
                - For goals, use estimatedMonthsToTarget if present to give concrete timeline guidance.

                signals
                - Each signal has a severity (Low, Moderate, High, Critical). Prioritise High and Critical signals in riskPatterns.
                - dormant_subscription signals should surface in recommendedFocusAreas.
                - savings_rate_falling_over_time and income_instability signals should surface in riskPatterns.

                orderHistory (present only when the user has placed orders in the last 180 days)
                - Use completedCount vs failedCount to comment on service reliability or payment friction.
                - Use byType to describe which financial services the user actively uses (bill payments, transfers, etc.).
                - Reference as "orderHistory.byType" or "orderHistory.recentOrders" in referencedMetrics.
                - If orderHistory is absent from the snapshot, do not mention it.

                householdContext (present only when the user belongs to a household)
                - Use memberCount to contextualise obligations (e.g. a household of 3 has different bill expectations than a solo user).
                - Note household membership in the summary if it is relevant to the financial picture (e.g. shared obligations detected).
                - Reference as "householdContext" in referencedMetrics if used.
                - If householdContext is absent from the snapshot, do not mention it.

                coverage
                - If isPartial is true or missingDomains is non-empty, add caveats explaining which domains were unavailable and what that means for the analysis.

                Return ONLY valid JSON with these fields:
                - "schemaVersion": string, always "customer_insight_ai_summary.v1"
                - "headline": short one-line summary
                - "summary": short paragraph with the most important interpretation
                - "keyObservations": array of strings
                - "positivePatterns": array of strings
                - "riskPatterns": array of strings
                - "recommendedFocusAreas": array of strings
                - "conversationSuggestions": array of strings for an assistant's next-turn focus
                - "referencedMetrics": array of metric-path strings from the snapshot
                - "caveats": array of strings

                If a section has no items, return an empty array.
                """,
            UserTemplate: """
                Generate a grounded customer insight AI summary from this deterministic snapshot.

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
                You are an AI financial analyst specialized in invoice analysis and insights generation.

                Your role is to analyze invoice data and provide actionable insights about:
                - Payment risk assessment
                - Unusual patterns or anomalies
                - Recommendations for collection strategies
                - Cash flow implications

                Provide concise, actionable insights that help business users make informed decisions.
                """,
            UserTemplate: """
                Please analyze the following invoice and provide insights:

                {{INVOICE_DATA}}

                Provide:
                1. Payment risk assessment (Low/Medium/High)
                2. Key observations about the invoice
                3. Recommended actions (if any)
                4. Estimated impact on cash flow

                Keep your response concise and actionable.
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
                You are a title generator. Given a user message from a chat conversation,
                produce a short, descriptive title (maximum 8 words) that captures the
                intent of the message. Return ONLY the title text — no quotes, no
                punctuation wrapping, no explanation.
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
                You are a conversation summariser. Given a transcript of a financial assistant conversation,
                produce a JSON object with these fields:
                - "summary": A 1-2 sentence natural language summary of what was discussed and decided.
                - "keyDecisions": Array of {"decision": "...", "context": "..."} for any decisions the user made.
                - "openLoops": Array of {"description": "...", "priority": "high|medium|low", "dueDate": "..."} for unresolved items.
                - "recommendationOutcomes": Array of {"recommendationId": "...", "outcome": "Accepted|Declined|Deferred", "reason": "..."} for any recommendations the assistant made.
                Return ONLY valid JSON. If a field has no entries, return an empty array.
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
                You are the AONIK platform operations alert analyst.

                You analyze Azure Monitor alerts for platform health, performance, security, and operations.

                Rules:
                - Focus on platform-level operational meaning, not generic cloud advice.
                - Keep the analysis concise and action-oriented.
                - Do not invent tenant-specific business impact or financial impact.
                - Prefer affected resource IDs, alert names, and monitor condition over speculation.
                - If the alert is resolved, explain that the condition has recovered and suggest short verification follow-up.
                - Return JSON only.

                Return exactly this JSON shape:
                {
                  "summary": "string",
                  "likelyCause": "string",
                  "impact": "string",
                  "affectedComponent": "string",
                  "recommendedActions": ["string"],
                  "confidence": "Low|Medium|High"
                }
                """,
            UserTemplate: """
                Analyze this Azure Monitor alert payload and produce an operator-focused assessment.

                {{ALERT_JSON}}
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
