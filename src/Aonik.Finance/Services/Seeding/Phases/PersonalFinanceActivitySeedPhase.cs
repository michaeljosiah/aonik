using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Persistence;

namespace Aonik.Finance.Services.Seeding.Phases;

/// <summary>
/// Seeds one year of personal-finance data (accounts, transactions, recurring
/// bills, subscriptions) for the two UK demo personas — Seamus Keane (struggling
/// profile: low income, high bills, high credit-card usage) and Mark Keane
/// (comfortable profile: high income, light commitments, light card usage).
///
/// The personas exist so the Simi sub-agents (insights / forecast / classify)
/// have realistic, varied data to read in the admin UI and (later) in Payabo.
///
/// Identity model: the personal-finance entities are scoped by <c>UserId</c>,
/// but no Auth0 user exists for these personas yet — the synthetic UserIds in
/// <c>finance-demo-ids.json#personalFinancePersonas</c> are used directly. A
/// future admin feature will let the operator invite a real Auth0 user and
/// rewrite all rows here from the synthetic UserId to the real one.
///
/// Idempotency: every seeded row has a deterministic Guid derived from
/// <c>(userId, stable-key)</c>. Re-running the seed updates existing rows
/// in place rather than creating duplicates.
/// </summary>
internal sealed class PersonalFinanceActivitySeedPhase
{
    private static readonly FinanceDemoSeedIds SeedIds = FinanceDemoSeedIds.Instance;

    private readonly FinanceDbContext _db;
    private readonly ICustomerInsightSnapshotService? _snapshotService;

    public PersonalFinanceActivitySeedPhase(FinanceDbContext db)
    {
        _db = db;
        _snapshotService = null;
    }

    /// <summary>
    /// DI ctor — also takes <see cref="ICustomerInsightSnapshotService"/> so
    /// the seed phase can generate a baseline customer-insight snapshot per
    /// persona. The pf-insights sub-agent uses <c>pf_list_snapshot_history</c>
    /// and <c>pf_compare_snapshots</c> to answer "why was last month tight?"
    /// — without a snapshot it reports <c>snapshot_unavailable</c> and the
    /// playground demo can't surface the seeded car-repair / takeaway
    /// anomalies.
    /// </summary>
    public PersonalFinanceActivitySeedPhase(FinanceDbContext db, ICustomerInsightSnapshotService snapshotService)
    {
        _db = db;
        _snapshotService = snapshotService;
    }

    public async Task<IReadOnlyList<string>> SeedAsync(
        DemoSeedContext context,
        Dictionary<string, object> results,
        CancellationToken cancellationToken)
    {
        var operations = new List<string>();
        var endDate = context.Now.Date;
        var startDate = endDate.AddYears(-1);

        var personas = new List<PersonaDefinition>
        {
            BuildSeamus(startDate, endDate),
            BuildMark(startDate, endDate)
        };

        foreach (var persona in personas)
        {
            await SeedPersonaAsync(persona, context, cancellationToken);
            operations.Add(
                $"Seeded personal finance for {persona.DisplayName}: " +
                $"{persona.Accounts.Count} accounts, " +
                $"{persona.Transactions.Count} transactions, " +
                $"{persona.RecurringBills.Count} recurring bills, " +
                $"{persona.Subscriptions.Count} subscriptions");
        }

        await _db.SaveChangesAsync(cancellationToken);

        // ── Customer insight snapshots ───────────────────────────────
        // Generate one baseline snapshot per persona so the pf-insights
        // sub-agent has something to read for `Why was last month tight?`
        // questions. Without this it reports `snapshot_unavailable` /
        // `data_unavailable` and the playground demo never reaches the
        // car-repair + takeaway anomaly story the seed is designed to tell.
        if (_snapshotService is not null)
        {
            foreach (var persona in personas)
            {
                try
                {
                    var snapshot = await _snapshotService.GenerateCurrentSnapshotAsync(persona.UserId, cancellationToken);
                    operations.Add(
                        $"Generated customer-insight snapshot for {persona.DisplayName} " +
                        $"(window {snapshot.WindowStartUtc:yyyy-MM-dd}..{snapshot.WindowEndUtc:yyyy-MM-dd}, v{snapshot.Version})");
                }
                catch (Exception ex)
                {
                    // Don't let a snapshot generation failure block the seed run.
                    // The transactions / bills / accounts have already saved.
                    operations.Add(
                        $"Customer-insight snapshot for {persona.DisplayName} failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        return operations;
    }

    // ── Persona orchestration ────────────────────────────────────────

    private async Task SeedPersonaAsync(
        PersonaDefinition persona,
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        await UpsertPersonalProfileAsync(persona, context, cancellationToken);

        foreach (var account in persona.Accounts)
        {
            await UpsertAccountAsync(persona, account, context, cancellationToken);
        }

        foreach (var tx in persona.Transactions)
        {
            await UpsertTransactionAsync(persona, tx, context, cancellationToken);
        }

        foreach (var bill in persona.RecurringBills)
        {
            await UpsertRecurringBillAsync(persona, bill, context, cancellationToken);
            // Both Bill (old payment-execution table) and PersonalRecurringBill
            // (new commitment-tracking table) get populated from the same source.
            // Tools like pf_get_upcoming_bills / pf_get_dashboard read the Bill
            // table while customer-insight snapshots + commitments aggregation
            // read PersonalRecurringBill, so we need both for Simi's full
            // toolset to surface coherent data.
            await UpsertBillAsync(persona, bill, context, cancellationToken);
        }

        foreach (var sub in persona.Subscriptions)
        {
            await UpsertSubscriptionAsync(persona, sub, context, cancellationToken);
        }
    }

    private async Task UpsertPersonalProfileAsync(
        PersonaDefinition persona,
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        var existing = await _db.PersonalProfiles
            .FirstOrDefaultAsync(p => p.Id == persona.PersonalProfileId, cancellationToken);

        if (existing == null)
        {
            _db.PersonalProfiles.Add(new PersonalProfile
            {
                Id = persona.PersonalProfileId,
                TenantId = context.TenantId,
                UserId = persona.UserId,
                PartyId = persona.PartyId,
                HouseholdId = null,
                CreatedAt = context.Now,
                CreatedBy = context.UserId
            });
        }
        else
        {
            existing.TenantId = context.TenantId;
            existing.UserId = persona.UserId;
            existing.PartyId = persona.PartyId;
            existing.UpdatedAt = context.Now;
            existing.UpdatedBy = context.UserId;
        }
    }

    private async Task UpsertAccountAsync(
        PersonaDefinition persona,
        AccountSeed account,
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        var existing = await _db.PersonalAccounts
            .FirstOrDefaultAsync(a => a.Id == account.AccountId, cancellationToken);

        if (existing == null)
        {
            _db.PersonalAccounts.Add(new PersonalAccount
            {
                Id = account.AccountId,
                TenantId = context.TenantId,
                UserId = persona.UserId,
                HouseholdId = null,
                Name = account.Name,
                AccountType = account.AccountType,
                AccountSubtype = account.AccountSubtype,
                Currency = "GBP",
                InstitutionName = account.InstitutionName,
                ExternalReference = $"demo-{account.AccountId:N}",
                Status = "Active",
                Last4 = account.Last4,
                CurrentBalance = account.CurrentBalance,
                BalanceAsOf = context.Now,
                IsArchived = false,
                OpenedAt = context.Now.AddYears(-3),
                CreatedAt = context.Now,
                CreatedBy = context.UserId
            });
        }
        else
        {
            existing.TenantId = context.TenantId;
            existing.UserId = persona.UserId;
            existing.Name = account.Name;
            existing.AccountType = account.AccountType;
            existing.AccountSubtype = account.AccountSubtype;
            existing.Currency = "GBP";
            existing.InstitutionName = account.InstitutionName;
            existing.Status = "Active";
            existing.Last4 = account.Last4;
            existing.CurrentBalance = account.CurrentBalance;
            existing.BalanceAsOf = context.Now;
            existing.IsArchived = false;
            existing.UpdatedAt = context.Now;
            existing.UpdatedBy = context.UserId;
        }
    }

    private async Task UpsertTransactionAsync(
        PersonaDefinition persona,
        TxSeed tx,
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        var txId = DeterministicGuid(persona.UserId, $"tx:{tx.SequenceKey}");

        var existing = await _db.PersonalTransactions
            .FirstOrDefaultAsync(t => t.Id == txId, cancellationToken);

        var transactionType = TransactionCategoryReference.ResolveTransactionType(tx.Category, tx.Amount);

        if (existing == null)
        {
            _db.PersonalTransactions.Add(new PersonalTransaction
            {
                Id = txId,
                TenantId = context.TenantId,
                UserId = persona.UserId,
                PersonalAccountId = tx.AccountId,
                SourceType = "DemoSeed",
                SourceId = txId,
                OccurredAt = tx.OccurredAt,
                Amount = tx.Amount,
                Currency = "GBP",
                Merchant = tx.Merchant,
                Description = tx.Description ?? tx.Merchant,
                TransactionType = transactionType,
                Category = tx.Category,
                SubCategory = tx.SubCategory,
                Confidence = 0.95m,
                CategorisedBy = "Seed",
                ClassificationMethod = "Seed",
                ClassifierVersion = "demo-1",
                ReviewStatus = "Confirmed",
                ReviewedAt = context.Now,
                ImportFingerprint = $"demo-seed:{persona.UserId:N}:{tx.SequenceKey}",
                TagsJson = "[]",
                CreatedAt = context.Now,
                CreatedBy = context.UserId
            });
        }
        else
        {
            existing.TenantId = context.TenantId;
            existing.UserId = persona.UserId;
            existing.PersonalAccountId = tx.AccountId;
            existing.OccurredAt = tx.OccurredAt;
            existing.Amount = tx.Amount;
            existing.Currency = "GBP";
            existing.Merchant = tx.Merchant;
            existing.Description = tx.Description ?? tx.Merchant;
            existing.TransactionType = transactionType;
            existing.Category = tx.Category;
            existing.SubCategory = tx.SubCategory;
            existing.UpdatedAt = context.Now;
            existing.UpdatedBy = context.UserId;
        }
    }

    private async Task UpsertRecurringBillAsync(
        PersonaDefinition persona,
        RecurringBillSeed bill,
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        var billId = DeterministicGuid(persona.UserId, $"recurring-bill:{bill.Key}");

        var existing = await _db.PersonalRecurringBills
            .FirstOrDefaultAsync(b => b.Id == billId, cancellationToken);

        if (existing == null)
        {
            _db.PersonalRecurringBills.Add(new PersonalRecurringBill
            {
                Id = billId,
                TenantId = context.TenantId,
                UserId = persona.UserId,
                PaidFromAccountId = bill.PaidFromAccountId,
                Payee = bill.Payee,
                Frequency = "Monthly",
                NextDueDate = bill.NextDueDate,
                ExpectedAmount = bill.ExpectedAmount,
                Currency = "GBP",
                Autopay = bill.Autopay,
                Status = "Active",
                VerificationStatus = "Confirmed",
                Origin = "Manual",
                Category = bill.Category,
                SubCategory = bill.SubCategory,
                ReminderDaysBefore = 3,
                GracePeriodDays = 5,
                LastObservedAt = bill.LastObservedAt,
                LastPaidAt = bill.LastObservedAt,
                LastPaidAmount = bill.ExpectedAmount,
                CreatedAt = context.Now,
                CreatedBy = context.UserId
            });
        }
        else
        {
            existing.TenantId = context.TenantId;
            existing.UserId = persona.UserId;
            existing.PaidFromAccountId = bill.PaidFromAccountId;
            existing.Payee = bill.Payee;
            existing.NextDueDate = bill.NextDueDate;
            existing.ExpectedAmount = bill.ExpectedAmount;
            existing.Currency = "GBP";
            existing.Autopay = bill.Autopay;
            existing.Status = "Active";
            existing.VerificationStatus = "Confirmed";
            existing.Category = bill.Category;
            existing.SubCategory = bill.SubCategory;
            existing.LastObservedAt = bill.LastObservedAt;
            existing.LastPaidAt = bill.LastObservedAt;
            existing.LastPaidAmount = bill.ExpectedAmount;
            existing.UpdatedAt = context.Now;
            existing.UpdatedBy = context.UserId;
        }
    }

    private async Task UpsertBillAsync(
        PersonaDefinition persona,
        RecurringBillSeed bill,
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        // Use a different seed-key prefix so the deterministic Guid is distinct
        // from the PersonalRecurringBill row, even though both rows mirror the
        // same conceptual obligation.
        var billId = DeterministicGuid(persona.UserId, $"bill:{bill.Key}");

        var existing = await _db.Bills
            .FirstOrDefaultAsync(b => b.Id == billId, cancellationToken);

        if (existing == null)
        {
            _db.Bills.Add(new Bill
            {
                Id = billId,
                TenantId = context.TenantId,
                UserId = persona.UserId,
                PaidFromAccountId = bill.PaidFromAccountId,
                Payee = bill.Payee,
                Frequency = "Monthly",
                NextDueDate = bill.NextDueDate,
                ExpectedAmount = bill.ExpectedAmount,
                Currency = "GBP",
                Autopay = bill.Autopay,
                LinkedInvoiceId = null,
                LinkedOrderId = null,
                Status = "Active",
                CreatedAt = context.Now,
                CreatedBy = context.UserId
            });
        }
        else
        {
            existing.TenantId = context.TenantId;
            existing.UserId = persona.UserId;
            existing.PaidFromAccountId = bill.PaidFromAccountId;
            existing.Payee = bill.Payee;
            existing.Frequency = "Monthly";
            existing.NextDueDate = bill.NextDueDate;
            existing.ExpectedAmount = bill.ExpectedAmount;
            existing.Currency = "GBP";
            existing.Autopay = bill.Autopay;
            existing.Status = "Active";
            existing.UpdatedAt = context.Now;
            existing.UpdatedBy = context.UserId;
        }
    }

    private async Task UpsertSubscriptionAsync(
        PersonaDefinition persona,
        SubscriptionSeed sub,
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        var subId = DeterministicGuid(persona.UserId, $"subscription:{sub.Key}");

        var existing = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subId, cancellationToken);

        if (existing == null)
        {
            _db.Subscriptions.Add(new Subscription
            {
                Id = subId,
                TenantId = context.TenantId,
                UserId = persona.UserId,
                Merchant = sub.Merchant,
                RenewalDate = sub.NextRenewal,
                ExpectedAmount = sub.ExpectedAmount,
                Currency = "GBP",
                Status = "Active",
                DetectedBy = "Seed",
                Frequency = "Monthly",
                PaidFromAccountId = sub.PaidFromAccountId,
                VerificationStatus = "Confirmed",
                Origin = "Manual",
                Autopay = true,
                Category = TransactionCategoryReference.Subscriptions,
                SubCategory = sub.SubCategory,
                LastObservedAt = sub.LastChargedAt,
                LastChargedAt = sub.LastChargedAt,
                LastChargedAmount = sub.ExpectedAmount,
                CreatedAt = context.Now,
                CreatedBy = context.UserId
            });
        }
        else
        {
            existing.TenantId = context.TenantId;
            existing.UserId = persona.UserId;
            existing.Merchant = sub.Merchant;
            existing.RenewalDate = sub.NextRenewal;
            existing.ExpectedAmount = sub.ExpectedAmount;
            existing.Currency = "GBP";
            existing.Status = "Active";
            existing.PaidFromAccountId = sub.PaidFromAccountId;
            existing.VerificationStatus = "Confirmed";
            existing.SubCategory = sub.SubCategory;
            existing.LastObservedAt = sub.LastChargedAt;
            existing.LastChargedAt = sub.LastChargedAt;
            existing.LastChargedAmount = sub.ExpectedAmount;
            existing.UpdatedAt = context.Now;
            existing.UpdatedBy = context.UserId;
        }
    }

    // ── Persona definitions ──────────────────────────────────────────

    private static PersonaDefinition BuildSeamus(DateTime startDate, DateTime endDate)
    {
        var ids = SeedIds.PersonalFinancePersonas;
        var currentId = ids.SeamusCurrentAccountId;
        var ccId = ids.SeamusCreditCardAccountId;
        var savingsId = ids.SeamusSavingsAccountId;

        var accounts = new List<AccountSeed>
        {
            // Current balance is deliberately low (~ one bus pass + groceries
            // away from overdraft) so the "Will I have enough for rent on
            // the 30th?" forecast question has to actually do the maths:
            // walk from today's balance, through the £75 CC min on the 22nd
            // and ~£200 of typical discretionary, then the £1,800 salary on
            // the 28th, and finally the £900 rent on the 30th. Answer should
            // land as "tight but yes, ~£600-700 buffer after rent".
            new(currentId, "Lloyds Current Account",      "Checking",    "current",      "Lloyds Bank",       "4421",   42.80m),
            new(ccId,      "Barclaycard Platinum",        "CreditCard",  "credit_card",  "Barclaycard",       "8819", -2_847.30m),
            new(savingsId, "Lloyds Easy Saver",           "Savings",     "savings",      "Lloyds Bank",       "4422",  187.20m)
        };

        var txs = new List<TxSeed>();
        var bills = new List<RecurringBillSeed>();
        var subs = new List<SubscriptionSeed>();

        // Walk forward month by month. Each month emits a fixed pattern of
        // recurring bills + a stochastic-looking spread of discretionary
        // transactions. The "stochastic" amounts are actually deterministic
        // — they vary based on a per-month seed so the data looks natural
        // across the year but is reproducible.
        var monthIndex = 0;
        for (var cursor = StartOfMonth(startDate); cursor <= endDate; cursor = cursor.AddMonths(1), monthIndex++)
        {
            var monthSeed = monthIndex * 31;

            // ── Income on 28th: salary £1,800
            AddIfInRange(txs, SafeDate(cursor, 28), -1, startDate, endDate, (date, key) => TxSeed.Income(
                date, 1_800m, "Manchester Logistics Ltd", TransactionCategoryReference.Income, "salary",
                currentId, $"sx-salary:{key}"));

            // Occasional side hustle quarter-on £180 (months 2,5,8,11 of cycle)
            if (monthIndex % 3 == 2)
            {
                AddIfInRange(txs, SafeDate(cursor, 14), -1, startDate, endDate, (date, key) => TxSeed.Income(
                    date, 180m, "Fiverr UK Ltd", TransactionCategoryReference.Income, "side_hustle",
                    currentId, $"sx-side:{key}"));
            }

            // ── Recurring direct debits (Housing / Bills / Fitness / Loan)
            // Rent is on the 30th deliberately — this is the date used in
            // the canonical demo question "Will I have enough for rent on
            // the 30th?". Don't move without updating the playground.
            AddRecurring(txs, cursor, 30, startDate, endDate,
                amount: 900m, payee: "Hollybush Property Mgmt",
                category: TransactionCategoryReference.Housing, sub: "rent",
                accountId: currentId, keyPrefix: "sx-rent");

            AddRecurring(txs, cursor, 5, startDate, endDate,
                amount: 130m, payee: "Manchester City Council",
                category: TransactionCategoryReference.Bills, sub: "council_tax",
                accountId: currentId, keyPrefix: "sx-counciltax");

            AddRecurring(txs, cursor, 10, startDate, endDate,
                amount: 85m, payee: "Octopus Energy",
                category: TransactionCategoryReference.Bills, sub: "electricity",
                accountId: currentId, keyPrefix: "sx-elec");

            AddRecurring(txs, cursor, 10, startDate, endDate,
                amount: 45m, payee: "Octopus Energy - Gas",
                category: TransactionCategoryReference.Bills, sub: "gas",
                accountId: currentId, keyPrefix: "sx-gas");

            AddRecurring(txs, cursor, 15, startDate, endDate,
                amount: 35m, payee: "United Utilities",
                category: TransactionCategoryReference.Bills, sub: "water",
                accountId: currentId, keyPrefix: "sx-water");

            AddRecurring(txs, cursor, 15, startDate, endDate,
                amount: 40m, payee: "Virgin Media Broadband",
                category: TransactionCategoryReference.Bills, sub: "internet",
                accountId: currentId, keyPrefix: "sx-broadband");

            AddRecurring(txs, cursor, 15, startDate, endDate,
                amount: 25m, payee: "Vodafone UK",
                category: TransactionCategoryReference.Bills, sub: "phone",
                accountId: currentId, keyPrefix: "sx-phone");

            AddRecurring(txs, cursor, 1, startDate, endDate,
                amount: 13.25m, payee: "TV Licensing",
                category: TransactionCategoryReference.Bills, sub: "tv_licence",
                accountId: currentId, keyPrefix: "sx-tvlicence");

            AddRecurring(txs, cursor, 1, startDate, endDate,
                amount: 19.99m, payee: "PureGym Manchester",
                category: TransactionCategoryReference.Fitness, sub: "gym",
                accountId: currentId, keyPrefix: "sx-gym");

            AddRecurring(txs, cursor, 22, startDate, endDate,
                amount: 75m, payee: "Barclaycard Minimum Payment",
                category: TransactionCategoryReference.LoanPayments, sub: "credit_card",
                accountId: currentId, keyPrefix: "sx-ccmin",
                description: "Direct debit — minimum payment to Barclaycard 8819");

            // ── Subscriptions (current account direct debits)
            AddRecurring(txs, cursor, 5, startDate, endDate,
                amount: 10.99m, payee: "Spotify UK",
                category: TransactionCategoryReference.Subscriptions, sub: "music",
                accountId: currentId, keyPrefix: "sx-spotify");

            AddRecurring(txs, cursor, 15, startDate, endDate,
                amount: 11.99m, payee: "Netflix UK",
                category: TransactionCategoryReference.Subscriptions, sub: "streaming",
                accountId: currentId, keyPrefix: "sx-netflix");

            // ── Groceries on current account — 4 trips/month at varied amounts
            string[] groceryMerchants = { "Tesco Express", "Lidl Manchester", "Sainsbury's Local", "Aldi Stretford" };
            for (var g = 0; g < 4; g++)
            {
                var day = 4 + g * 7;
                var amount = 55m + ((monthSeed + g) % 5) * 7m; // £55-£83
                AddIfInRange(txs, SafeDate(cursor, day), g, startDate, endDate, (date, key) => TxSeed.Expense(
                    date, -amount, groceryMerchants[g % groceryMerchants.Length],
                    TransactionCategoryReference.Groceries, "supermarket", currentId,
                    $"sx-groc:{key}"));
            }

            // Fuel — 2 trips/month
            string[] fuelMerchants = { "Esso Salford", "BP Manchester Rd" };
            for (var f = 0; f < 2; f++)
            {
                var day = 9 + f * 14;
                var amount = 48m + ((monthSeed + f) % 4) * 6m;
                AddIfInRange(txs, SafeDate(cursor, day), f, startDate, endDate, (date, key) => TxSeed.Expense(
                    date, -amount, fuelMerchants[f % fuelMerchants.Length],
                    TransactionCategoryReference.Transport, "fuel", currentId,
                    $"sx-fuel:{key}"));
            }

            // Cash withdrawal
            AddIfInRange(txs, SafeDate(cursor, 12), -1, startDate, endDate, (date, key) => TxSeed.Expense(
                date, -80m, "Lloyds ATM Withdrawal",
                TransactionCategoryReference.Other, null, currentId, $"sx-atm:{key}"));

            // Bus pass top-up (Transport - public transit)
            AddIfInRange(txs, SafeDate(cursor, 3), -1, startDate, endDate, (date, key) => TxSeed.Expense(
                date, -30m, "Bee Network Manchester",
                TransactionCategoryReference.Transport, "public_transit", currentId,
                $"sx-bus:{key}"));

            // ── Credit card spending (heavy — only minimum gets paid)
            // Takeaway × 10 per month — high CC usage
            string[] takeawayMerchants = { "Just Eat", "Uber Eats", "Deliveroo", "Just Eat", "Uber Eats", "Domino's Pizza", "KFC Salford", "Deliveroo", "Just Eat", "Uber Eats" };
            for (var t = 0; t < 10; t++)
            {
                var day = 2 + (t * 3) % 27;
                var amount = 14m + ((monthSeed + t) % 8) * 3m; // £14-£35
                AddIfInRange(txs, SafeDate(cursor, day), t, startDate, endDate, (date, key) => TxSeed.Expense(
                    date, -amount, takeawayMerchants[t % takeawayMerchants.Length],
                    TransactionCategoryReference.EatingOut, "delivery", ccId, $"sx-take:{key}"));
            }

            // Coffee/lunch × 10 per month
            string[] cafeMerchants = { "Pret a Manger", "Costa Coffee", "Greggs", "Starbucks", "Pret a Manger", "Greggs", "Costa Coffee", "Caffe Nero", "Pret a Manger", "Greggs" };
            for (var c = 0; c < 10; c++)
            {
                var day = 3 + (c * 2 + 1) % 27;
                var amount = 6m + ((monthSeed + c) % 4) * 2m; // £6-£14
                AddIfInRange(txs, SafeDate(cursor, day), c, startDate, endDate, (date, key) => TxSeed.Expense(
                    date, -amount, cafeMerchants[c % cafeMerchants.Length],
                    TransactionCategoryReference.EatingOut, "cafe", ccId, $"sx-cafe:{key}"));
            }

            // Restaurants/pubs × 4 per month
            string[] restaurantMerchants = { "The Beech Pub", "Wagamama Spinningfields", "Nando's", "The Northern Quarter" };
            for (var r = 0; r < 4; r++)
            {
                var day = 6 + r * 7;
                var amount = 28m + ((monthSeed + r) % 5) * 5m;
                AddIfInRange(txs, SafeDate(cursor, day), r, startDate, endDate, (date, key) => TxSeed.Expense(
                    date, -amount, restaurantMerchants[r % restaurantMerchants.Length],
                    TransactionCategoryReference.EatingOut, "restaurant", ccId, $"sx-rest:{key}"));
            }

            // Online shopping × 3 per month
            string[] shoppingMerchants = { "Amazon UK", "ASOS", "eBay UK" };
            string[] shoppingSubs = { "online", "clothing", "online" };
            for (var s = 0; s < 3; s++)
            {
                var day = 7 + s * 8;
                var amount = 25m + ((monthSeed + s * 11) % 7) * 12m; // £25-£97
                AddIfInRange(txs, SafeDate(cursor, day), s, startDate, endDate, (date, key) => TxSeed.Expense(
                    date, -amount, shoppingMerchants[s % shoppingMerchants.Length],
                    TransactionCategoryReference.Shopping, shoppingSubs[s % shoppingSubs.Length],
                    ccId, $"sx-shop:{key}"));
            }

            // Ride hailing × 3
            for (var u = 0; u < 3; u++)
            {
                var day = 11 + u * 7;
                var amount = 12m + ((monthSeed + u) % 5) * 3m;
                AddIfInRange(txs, SafeDate(cursor, day), u, startDate, endDate, (date, key) => TxSeed.Expense(
                    date, -amount, "Uber",
                    TransactionCategoryReference.Transport, "ride_hailing", ccId, $"sx-uber:{key}"));
            }

            // App store / digital top-ups
            AddIfInRange(txs, SafeDate(cursor, 19), -1, startDate, endDate, (date, key) => TxSeed.Expense(
                date, -14.99m, "Apple Services",
                TransactionCategoryReference.Subscriptions, "software", ccId, $"sx-apple:{key}"));

            // Pub/bar Friday night × 4
            for (var p = 0; p < 4; p++)
            {
                var day = 8 + p * 7;
                var amount = 22m + ((monthSeed + p) % 5) * 4m;
                AddIfInRange(txs, SafeDate(cursor, day), p, startDate, endDate, (date, key) => TxSeed.Expense(
                    date, -amount, "The Corner Pub",
                    TransactionCategoryReference.EatingOut, "restaurant", ccId, $"sx-pub:{key}"));
            }

            // Occasional larger clothing splurge (every 3 months)
            if (monthIndex % 3 == 1)
            {
                AddIfInRange(txs, SafeDate(cursor, 18), -1, startDate, endDate, (date, key) => TxSeed.Expense(
                    date, -145m, "JD Sports",
                    TransactionCategoryReference.Shopping, "clothing", ccId, $"sx-splurge:{key}"));
            }
        }

        // ── "Why was last month tight?" anomalies ────────────────────
        // Inject two clear signals into the prior calendar month so the
        // insights agent has something concrete to surface:
        //   (a) one-off £420 car repair on the 12th (Transport spike)
        //   (b) five extra takeaway orders on the credit card spread
        //       across the month (EatingOut spike, ~50% above baseline)
        // Both are date-anchored relative to context.Now via endDate, so
        // re-seeding always puts the anomalies in "last calendar month"
        // regardless of when the seed actually runs.
        var sxLastMonthStart = StartOfMonth(endDate).AddMonths(-1);

        AddIfInRange(txs, SafeDate(sxLastMonthStart, 12), -1, startDate, endDate,
            (date, key) => TxSeed.Expense(
                date, -420m, "AutoFix Garage",
                TransactionCategoryReference.Transport, "car_maintenance", currentId,
                $"sx-anomaly-carrepair:{key}",
                description: "Unexpected car repair - clutch replacement"));

        string[] extraTakeawayMerchants = { "Deliveroo", "Just Eat", "Uber Eats", "Domino's Pizza", "Just Eat" };
        for (var i = 0; i < 5; i++)
        {
            var day = 6 + i * 4;
            var amount = 28m + i * 2m;
            var merchantIndex = i;
            AddIfInRange(txs, SafeDate(sxLastMonthStart, day), merchantIndex, startDate, endDate,
                (date, key) => TxSeed.Expense(
                    date, -amount, extraTakeawayMerchants[merchantIndex],
                    TransactionCategoryReference.EatingOut, "delivery", ccId,
                    $"sx-anomaly-takeaway:{key}"));
        }

        // ── Recurring bills (Simi reads these)
        var sxLastEom = endDate;
        bills.AddRange(new[]
        {
            new RecurringBillSeed("rent",       "Hollybush Property Mgmt", currentId, 900m,   NextDate(endDate, 30), TransactionCategoryReference.Housing, "rent",          sxLastEom),
            new RecurringBillSeed("council",    "Manchester City Council", currentId, 130m,   NextDate(endDate, 5),  TransactionCategoryReference.Bills,   "council_tax",   sxLastEom),
            new RecurringBillSeed("elec",       "Octopus Energy",          currentId, 85m,    NextDate(endDate, 10), TransactionCategoryReference.Bills,   "electricity",   sxLastEom),
            new RecurringBillSeed("gas",        "Octopus Energy - Gas",    currentId, 45m,    NextDate(endDate, 10), TransactionCategoryReference.Bills,   "gas",           sxLastEom),
            new RecurringBillSeed("water",      "United Utilities",        currentId, 35m,    NextDate(endDate, 15), TransactionCategoryReference.Bills,   "water",         sxLastEom),
            new RecurringBillSeed("broadband",  "Virgin Media Broadband",  currentId, 40m,    NextDate(endDate, 15), TransactionCategoryReference.Bills,   "internet",      sxLastEom),
            new RecurringBillSeed("phone",      "Vodafone UK",             currentId, 25m,    NextDate(endDate, 15), TransactionCategoryReference.Bills,   "phone",         sxLastEom),
            new RecurringBillSeed("tvlicence",  "TV Licensing",            currentId, 13.25m, NextDate(endDate, 1),  TransactionCategoryReference.Bills,   "tv_licence",    sxLastEom),
            new RecurringBillSeed("gym",        "PureGym Manchester",      currentId, 19.99m, NextDate(endDate, 1),  TransactionCategoryReference.Fitness, "gym",           sxLastEom),
            new RecurringBillSeed("ccmin",      "Barclaycard Minimum",     currentId, 75m,    NextDate(endDate, 22), TransactionCategoryReference.LoanPayments, "credit_card", sxLastEom, Autopay: true),
        });

        // ── Subscriptions
        subs.AddRange(new[]
        {
            new SubscriptionSeed("spotify",  "Spotify UK", currentId, 10.99m, NextDate(endDate, 5),  "music",     sxLastEom),
            new SubscriptionSeed("netflix",  "Netflix UK", currentId, 11.99m, NextDate(endDate, 15), "streaming", sxLastEom),
            new SubscriptionSeed("appleone", "Apple Services", ccId,   14.99m, NextDate(endDate, 19), "software", sxLastEom)
        });

        return new PersonaDefinition(
            DisplayName: "Seamus Keane",
            PartyId: ids.SeamusKeanePartyId,
            UserId: ids.SeamusKeaneUserId,
            PersonalProfileId: ids.SeamusKeanePersonalProfileId,
            Accounts: accounts,
            Transactions: txs,
            RecurringBills: bills,
            Subscriptions: subs);
    }

    private static PersonaDefinition BuildMark(DateTime startDate, DateTime endDate)
    {
        var ids = SeedIds.PersonalFinancePersonas;
        var currentId = ids.MarkCurrentAccountId;
        var ccId = ids.MarkCreditCardAccountId;
        var savingsId = ids.MarkSavingsAccountId;

        var accounts = new List<AccountSeed>
        {
            new(currentId, "Monzo Current Account",      "Checking",    "current",      "Monzo Bank",  "1107", 3_842.55m),
            new(ccId,      "Amex Platinum Cashback",    "CreditCard",  "credit_card",  "American Express", "1006", 0m),
            new(savingsId, "Marcus by Goldman Sachs",    "Savings",     "savings",      "Goldman Sachs",   "9921", 28_410.00m)
        };

        var txs = new List<TxSeed>();
        var bills = new List<RecurringBillSeed>();
        var subs = new List<SubscriptionSeed>();

        var monthIndex = 0;
        for (var cursor = StartOfMonth(startDate); cursor <= endDate; cursor = cursor.AddMonths(1), monthIndex++)
        {
            var monthSeed = monthIndex * 37;

            // Salary — 25th, £5,500
            AddIfInRange(txs, SafeDate(cursor, 25), -1, startDate, endDate, (date, key) => TxSeed.Income(
                date, 5_500m, "Pixelflow Software Ltd", TransactionCategoryReference.Income, "salary",
                currentId, $"mk-salary:{key}"));

            // Annual bonus — March
            if (cursor.Month == 3)
            {
                AddIfInRange(txs, SafeDate(cursor, 28), -1, startDate, endDate, (date, key) => TxSeed.Income(
                    date, 4_000m, "Pixelflow Software Ltd (Bonus)", TransactionCategoryReference.Income, "salary",
                    currentId, $"mk-bonus:{key}"));
            }

            // ── Recurring direct debits
            AddRecurring(txs, cursor, 1, startDate, endDate,
                amount: 1_400m, payee: "Halifax Mortgage",
                category: TransactionCategoryReference.Housing, sub: "mortgage",
                accountId: currentId, keyPrefix: "mk-mortgage");

            AddRecurring(txs, cursor, 5, startDate, endDate,
                amount: 180m, payee: "Haringey Council",
                category: TransactionCategoryReference.Bills, sub: "council_tax",
                accountId: currentId, keyPrefix: "mk-counciltax");

            AddRecurring(txs, cursor, 10, startDate, endDate,
                amount: 75m, payee: "Octopus Energy",
                category: TransactionCategoryReference.Bills, sub: "electricity",
                accountId: currentId, keyPrefix: "mk-elec");

            AddRecurring(txs, cursor, 10, startDate, endDate,
                amount: 50m, payee: "Octopus Energy - Gas",
                category: TransactionCategoryReference.Bills, sub: "gas",
                accountId: currentId, keyPrefix: "mk-gas");

            AddRecurring(txs, cursor, 15, startDate, endDate,
                amount: 30m, payee: "Thames Water",
                category: TransactionCategoryReference.Bills, sub: "water",
                accountId: currentId, keyPrefix: "mk-water");

            AddRecurring(txs, cursor, 15, startDate, endDate,
                amount: 45m, payee: "BT Broadband",
                category: TransactionCategoryReference.Bills, sub: "internet",
                accountId: currentId, keyPrefix: "mk-broadband");

            AddRecurring(txs, cursor, 15, startDate, endDate,
                amount: 30m, payee: "EE Mobile",
                category: TransactionCategoryReference.Bills, sub: "phone",
                accountId: currentId, keyPrefix: "mk-phone");

            AddRecurring(txs, cursor, 1, startDate, endDate,
                amount: 13.25m, payee: "TV Licensing",
                category: TransactionCategoryReference.Bills, sub: "tv_licence",
                accountId: currentId, keyPrefix: "mk-tvlicence");

            AddRecurring(txs, cursor, 1, startDate, endDate,
                amount: 75m, payee: "Virgin Active Crouch End",
                category: TransactionCategoryReference.Fitness, sub: "gym",
                accountId: currentId, keyPrefix: "mk-gym");

            // Pension contribution
            AddRecurring(txs, cursor, 26, startDate, endDate,
                amount: 500m, payee: "Aviva Pension",
                category: TransactionCategoryReference.Investments, sub: "pension",
                accountId: currentId, keyPrefix: "mk-pension");

            // Savings transfer
            AddRecurring(txs, cursor, 26, startDate, endDate,
                amount: 1_500m, payee: "Marcus Savings",
                category: TransactionCategoryReference.Savings, sub: "emergency_fund",
                accountId: currentId, keyPrefix: "mk-savings",
                description: "Auto-transfer to Marcus by Goldman Sachs");

            // Charity
            AddRecurring(txs, cursor, 5, startDate, endDate,
                amount: 50m, payee: "Cancer Research UK",
                category: TransactionCategoryReference.Charity, sub: "donation",
                accountId: currentId, keyPrefix: "mk-charity");

            // ── Subscriptions
            AddRecurring(txs, cursor, 5, startDate, endDate,
                amount: 17.99m, payee: "Spotify Family",
                category: TransactionCategoryReference.Subscriptions, sub: "music",
                accountId: currentId, keyPrefix: "mk-spotify");

            AddRecurring(txs, cursor, 15, startDate, endDate,
                amount: 17.99m, payee: "Netflix Premium",
                category: TransactionCategoryReference.Subscriptions, sub: "streaming",
                accountId: currentId, keyPrefix: "mk-netflix");

            AddRecurring(txs, cursor, 10, startDate, endDate,
                amount: 8.99m, payee: "Disney+",
                category: TransactionCategoryReference.Subscriptions, sub: "streaming",
                accountId: currentId, keyPrefix: "mk-disney");

            // ── Groceries — 4 trips/month, higher-end stores
            string[] groceryMerchants = { "Waitrose Crouch End", "M&S Food Hall", "Sainsbury's Hornsey", "Whole Foods Highgate" };
            for (var g = 0; g < 4; g++)
            {
                var day = 4 + g * 7;
                var amount = 90m + ((monthSeed + g) % 5) * 10m; // £90-£130
                AddIfInRange(txs, SafeDate(cursor, day), g, startDate, endDate, (date, key) => TxSeed.Expense(
                    date, -amount, groceryMerchants[g % groceryMerchants.Length],
                    TransactionCategoryReference.Groceries, "supermarket", currentId,
                    $"mk-groc:{key}"));
            }

            // Fuel — 3 trips/month
            for (var f = 0; f < 3; f++)
            {
                var day = 7 + f * 10;
                var amount = 68m + ((monthSeed + f) % 4) * 5m;
                AddIfInRange(txs, SafeDate(cursor, day), f, startDate, endDate, (date, key) => TxSeed.Expense(
                    date, -amount, "Shell V-Power",
                    TransactionCategoryReference.Transport, "fuel", currentId, $"mk-fuel:{key}"));
            }

            // ── Credit card (paid in full monthly — see settlement below)
            // Restaurants × 6
            string[] restaurantMerchants = { "Dishoom King's Cross", "The Wolseley", "Padella Borough", "Hawksmoor Spitalfields", "Sketch Mayfair", "St. John Smithfield" };
            for (var r = 0; r < 6; r++)
            {
                var day = 3 + r * 4;
                var amount = 55m + ((monthSeed + r) % 6) * 8m; // £55-£95
                AddIfInRange(txs, SafeDate(cursor, day), r, startDate, endDate, (date, key) => TxSeed.Expense(
                    date, -amount, restaurantMerchants[r % restaurantMerchants.Length],
                    TransactionCategoryReference.EatingOut, "restaurant", ccId, $"mk-rest:{key}"));
            }

            // Online shopping × 3
            string[] shoppingMerchants = { "John Lewis", "Amazon UK", "Apple Store Online" };
            string[] shoppingSubs = { "department_store", "online", "electronics" };
            for (var s = 0; s < 3; s++)
            {
                var day = 9 + s * 8;
                var amount = 60m + ((monthSeed + s * 7) % 8) * 18m; // £60-£186
                AddIfInRange(txs, SafeDate(cursor, day), s, startDate, endDate, (date, key) => TxSeed.Expense(
                    date, -amount, shoppingMerchants[s % shoppingMerchants.Length],
                    TransactionCategoryReference.Shopping, shoppingSubs[s % shoppingSubs.Length],
                    ccId, $"mk-shop:{key}"));
            }

            // Travel/hotel — every 2 months
            if (monthIndex % 2 == 0)
            {
                AddIfInRange(txs, SafeDate(cursor, 12), -1, startDate, endDate, (date, key) => TxSeed.Expense(
                    date, -240m, "Booking.com",
                    TransactionCategoryReference.Travel, "hotel", ccId, $"mk-hotel:{key}"));
            }

            // Pubs/bars × 3
            for (var p = 0; p < 3; p++)
            {
                var day = 6 + p * 8;
                var amount = 30m + ((monthSeed + p) % 5) * 6m;
                AddIfInRange(txs, SafeDate(cursor, day), p, startDate, endDate, (date, key) => TxSeed.Expense(
                    date, -amount, "The Lion Crouch End",
                    TransactionCategoryReference.EatingOut, "restaurant", ccId, $"mk-pub:{key}"));
            }

            // Ride hailing × 2
            for (var u = 0; u < 2; u++)
            {
                var day = 14 + u * 10;
                var amount = 14m + ((monthSeed + u) % 5) * 3m;
                AddIfInRange(txs, SafeDate(cursor, day), u, startDate, endDate, (date, key) => TxSeed.Expense(
                    date, -amount, "Uber",
                    TransactionCategoryReference.Transport, "ride_hailing", ccId, $"mk-uber:{key}"));
            }

            // Credit-card settlement on 28th — pays off CC in full
            // For simplicity we synthesise a single settlement amount close to
            // the monthly spend (Mark pays in full).
            var settlementAmount = 900m + ((monthSeed) % 5) * 50m; // £900-£1100
            AddIfInRange(txs, SafeDate(cursor, 28), -1, startDate, endDate, (date, key) => TxSeed.Expense(
                date, -settlementAmount, "Amex Card Payment",
                TransactionCategoryReference.LoanPayments, "credit_card", currentId,
                $"mk-ccpay:{key}",
                description: "Direct debit — full balance payment to Amex 1006"));
            // Mirror as a credit on the CC account (positive amount, transfer in)
            AddIfInRange(txs, SafeDate(cursor, 28), -1, startDate, endDate, (date, key) => new TxSeed(
                OccurredAt: date,
                Amount: settlementAmount,
                Merchant: "Payment from Monzo",
                Category: TransactionCategoryReference.TransferIn,
                SubCategory: "own_account",
                AccountId: ccId,
                SequenceKey: $"mk-ccsettle:{key}",
                Description: "Auto-pay received from Monzo current account"));
        }

        var mkLastEom = endDate;
        bills.AddRange(new[]
        {
            new RecurringBillSeed("mortgage",   "Halifax Mortgage",         currentId, 1_400m, NextDate(endDate, 1),  TransactionCategoryReference.Housing, "mortgage",      mkLastEom),
            new RecurringBillSeed("council",    "Haringey Council",         currentId, 180m,   NextDate(endDate, 5),  TransactionCategoryReference.Bills,   "council_tax",   mkLastEom),
            new RecurringBillSeed("elec",       "Octopus Energy",           currentId, 75m,    NextDate(endDate, 10), TransactionCategoryReference.Bills,   "electricity",   mkLastEom),
            new RecurringBillSeed("gas",        "Octopus Energy - Gas",     currentId, 50m,    NextDate(endDate, 10), TransactionCategoryReference.Bills,   "gas",           mkLastEom),
            new RecurringBillSeed("water",      "Thames Water",             currentId, 30m,    NextDate(endDate, 15), TransactionCategoryReference.Bills,   "water",         mkLastEom),
            new RecurringBillSeed("broadband",  "BT Broadband",             currentId, 45m,    NextDate(endDate, 15), TransactionCategoryReference.Bills,   "internet",      mkLastEom),
            new RecurringBillSeed("phone",      "EE Mobile",                currentId, 30m,    NextDate(endDate, 15), TransactionCategoryReference.Bills,   "phone",         mkLastEom),
            new RecurringBillSeed("tvlicence",  "TV Licensing",             currentId, 13.25m, NextDate(endDate, 1),  TransactionCategoryReference.Bills,   "tv_licence",    mkLastEom),
            new RecurringBillSeed("gym",        "Virgin Active Crouch End", currentId, 75m,    NextDate(endDate, 1),  TransactionCategoryReference.Fitness, "gym",           mkLastEom),
            new RecurringBillSeed("pension",    "Aviva Pension",            currentId, 500m,   NextDate(endDate, 26), TransactionCategoryReference.Investments, "pension",   mkLastEom),
            new RecurringBillSeed("charity",    "Cancer Research UK",       currentId, 50m,    NextDate(endDate, 5),  TransactionCategoryReference.Charity, "donation",      mkLastEom),
        });

        subs.AddRange(new[]
        {
            new SubscriptionSeed("spotify", "Spotify Family",  currentId, 17.99m, NextDate(endDate, 5),  "music",     mkLastEom),
            new SubscriptionSeed("netflix", "Netflix Premium", currentId, 17.99m, NextDate(endDate, 15), "streaming", mkLastEom),
            new SubscriptionSeed("disney",  "Disney+",         currentId, 8.99m,  NextDate(endDate, 10), "streaming", mkLastEom)
        });

        return new PersonaDefinition(
            DisplayName: "Mark Keane",
            PartyId: ids.MarkKeanePartyId,
            UserId: ids.MarkKeaneUserId,
            PersonalProfileId: ids.MarkKeanePersonalProfileId,
            Accounts: accounts,
            Transactions: txs,
            RecurringBills: bills,
            Subscriptions: subs);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static DateTime StartOfMonth(DateTime date) => new(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Returns a date on <paramref name="day"/> of the month of <paramref name="cursor"/>,
    /// clamping to the last day of the month if the requested day exceeds it
    /// (so day=31 in February returns the 28th/29th).
    /// </summary>
    private static DateTime SafeDate(DateTime cursor, int day)
    {
        var lastDay = DateTime.DaysInMonth(cursor.Year, cursor.Month);
        var actualDay = Math.Min(day, lastDay);
        return new DateTime(cursor.Year, cursor.Month, actualDay, 9, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>
    /// Returns the next future occurrence of <paramref name="day"/>-of-month
    /// relative to <paramref name="now"/>. If <paramref name="day"/> has
    /// already passed this month, jumps to next month.
    /// </summary>
    private static DateTime NextDate(DateTime now, int day)
    {
        var thisMonth = SafeDate(now, day);
        if (thisMonth >= now)
        {
            return thisMonth;
        }
        return SafeDate(now.AddMonths(1), day);
    }

    private static void AddIfInRange(
        List<TxSeed> txs,
        DateTime date,
        int slotIndex,
        DateTime startDate,
        DateTime endDate,
        Func<DateTime, string, TxSeed> build)
    {
        if (date < startDate || date > endDate)
        {
            return;
        }
        var key = $"{date:yyyyMMdd}-{slotIndex}";
        txs.Add(build(date, key));
    }

    private static void AddRecurring(
        List<TxSeed> txs,
        DateTime cursor,
        int dayOfMonth,
        DateTime startDate,
        DateTime endDate,
        decimal amount,
        string payee,
        string category,
        string? sub,
        Guid accountId,
        string keyPrefix,
        string? description = null)
    {
        var date = SafeDate(cursor, dayOfMonth);
        AddIfInRange(txs, date, -1, startDate, endDate, (d, key) => TxSeed.Expense(
            d, -amount, payee, category, sub, accountId, $"{keyPrefix}:{key}", description));
    }

    /// <summary>
    /// Stable Guid derived from <paramref name="seed"/> + <paramref name="key"/> via SHA-1.
    /// Identical inputs always produce the same Guid → re-running the seed
    /// updates existing rows rather than duplicating them.
    /// </summary>
    private static Guid DeterministicGuid(Guid seed, string key)
    {
        Span<byte> input = stackalloc byte[16 + Encoding.UTF8.GetByteCount(key)];
        seed.TryWriteBytes(input[..16]);
        Encoding.UTF8.GetBytes(key, input[16..]);
        Span<byte> hash = stackalloc byte[20];
        SHA1.HashData(input, hash);
        return new Guid(hash[..16]);
    }

    // ── Inner seed types ─────────────────────────────────────────────

    private sealed record PersonaDefinition(
        string DisplayName,
        Guid PartyId,
        Guid UserId,
        Guid PersonalProfileId,
        IReadOnlyList<AccountSeed> Accounts,
        IReadOnlyList<TxSeed> Transactions,
        IReadOnlyList<RecurringBillSeed> RecurringBills,
        IReadOnlyList<SubscriptionSeed> Subscriptions);

    private sealed record AccountSeed(
        Guid AccountId,
        string Name,
        string AccountType,
        string AccountSubtype,
        string InstitutionName,
        string Last4,
        decimal CurrentBalance);

    /// <summary>
    /// A single transaction to be upserted. Amount is signed: negative for
    /// expenses, positive for income / transfer-in. SequenceKey must be stable
    /// across reseeds so the deterministic Guid stays consistent.
    /// </summary>
    private sealed record TxSeed(
        DateTime OccurredAt,
        decimal Amount,
        string Merchant,
        string Category,
        string? SubCategory,
        Guid AccountId,
        string SequenceKey,
        string? Description = null)
    {
        public static TxSeed Income(DateTime date, decimal amount, string merchant, string category, string? sub, Guid accountId, string key)
            => new(date, amount, merchant, category, sub, accountId, key);

        public static TxSeed Expense(DateTime date, decimal amount, string merchant, string category, string? sub, Guid accountId, string key, string? description = null)
            => new(date, amount, merchant, category, sub, accountId, key, description);
    }

    private sealed record RecurringBillSeed(
        string Key,
        string Payee,
        Guid PaidFromAccountId,
        decimal ExpectedAmount,
        DateTime NextDueDate,
        string Category,
        string SubCategory,
        DateTime LastObservedAt,
        bool Autopay = true);

    private sealed record SubscriptionSeed(
        string Key,
        string Merchant,
        Guid PaidFromAccountId,
        decimal ExpectedAmount,
        DateTime NextRenewal,
        string SubCategory,
        DateTime LastChargedAt);
}
