using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Entities;

namespace Aonik.PersonalFinance.Services.CustomerInsight;

/// <summary>
/// Computes a deterministic SHA-256 source hash over the canonicalised
/// inputs (tenant/user/window/coverage and the normalised lists of accounts,
/// transactions, bills, subscriptions, budgets and goals). Stable across runs
/// for the same inputs - used for snapshot equality and idempotency checks.
/// Also exposes the canonical currency-collection helper used by the snapshot
/// document.
/// </summary>
internal static class CustomerInsightSourceHasher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string ComputeSourceHash(
        Guid tenantId,
        Guid userId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        CustomerInsightCoverage coverage,
        IReadOnlyList<PersonalAccount> accounts,
        IReadOnlyList<NormalizedTransaction> transactions,
        IReadOnlyList<Bill> bills,
        IReadOnlyList<Subscription> subscriptions,
        IReadOnlyList<Budget> budgets,
        IReadOnlyList<Goal> goals)
    {
        var hashEnvelope = new
        {
            TenantId = tenantId,
            UserId = userId,
            WindowStartUtc = windowStartUtc,
            WindowEndUtc = windowEndUtc,
            GeneratorVersion = CustomerInsightSnapshotContract.GeneratorVersion,
            SchemaVersion = CustomerInsightSnapshotContract.SchemaVersion,
            DeterministicConfig = new
            {
                CustomerInsightSnapshotContract.OperationalWindowDays,
                CustomerInsightSnapshotContract.TrendWindowDays,
                CustomerInsightSnapshotContract.BehaviourWindowDays,
                CustomerInsightSnapshotContract.ObligationsLookaheadDays,
                CustomerInsightSnapshotContract.BudgetPressureThresholdPercent,
                MonetaryPolicy = CustomerInsightSnapshotContract.MonetaryPolicyNativeCurrency,
                TransferPolicy = CustomerInsightSnapshotContract.TransferPolicyNormalizedTransfers
            },
            Coverage = new
            {
                coverage.IsPartial,
                AvailableDomains = coverage.AvailableDomains.OrderBy(x => x).ToList(),
                MissingDomains = coverage.MissingDomains.OrderBy(x => x).ToList(),
                OmittedSections = coverage.OmittedSections.OrderBy(x => x).ToList()
            },
            Accounts = accounts
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    Currency = CustomerInsightNormalization.NormalizeCurrency(x.Currency),
                    Name = CustomerInsightNormalization.NormalizeKey(x.Name),
                    AccountType = CustomerInsightNormalization.NormalizeKey(x.AccountType),
                    Status = CustomerInsightNormalization.NormalizeKey(x.Status),
                    x.IsArchived,
                    x.CurrentBalance,
                    x.BalanceAsOf
                })
                .ToList(),
            Transactions = transactions
                .OrderBy(x => x.OccurredAtUtc)
                .ThenBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.PersonalAccountId,
                    x.OccurredAtUtc,
                    x.Amount,
                    x.Currency,
                    x.Category,
                    x.SubCategory,
                    x.NormalizedKind,
                    Merchant = x.MerchantKey,
                    Source = x.SourceKey
                })
                .ToList(),
            Bills = bills
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    Payee = CustomerInsightNormalization.NormalizeKey(x.Payee),
                    Currency = CustomerInsightNormalization.NormalizeCurrency(x.Currency),
                    x.ExpectedAmount,
                    x.NextDueDate,
                    Frequency = CustomerInsightNormalization.NormalizeKey(x.Frequency),
                    Status = CustomerInsightNormalization.NormalizeKey(x.Status),
                    x.LinkedOrderId
                })
                .ToList(),
            Subscriptions = subscriptions
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    Merchant = CustomerInsightNormalization.NormalizeKey(x.Merchant),
                    Currency = CustomerInsightNormalization.NormalizeCurrency(x.Currency),
                    x.ExpectedAmount,
                    x.RenewalDate,
                    Status = CustomerInsightNormalization.NormalizeKey(x.Status)
                })
                .ToList(),
            Budgets = budgets
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.PeriodStart,
                    PeriodType = CustomerInsightNormalization.NormalizeKey(x.PeriodType),
                    Status = CustomerInsightNormalization.NormalizeKey(x.Status),
                    Lines = x.Lines
                        .OrderBy(y => y.Id)
                        .Select(y => new
                        {
                            y.Id,
                            Category = CustomerInsightNormalization.NormalizeKey(y.Category),
                            Currency = CustomerInsightNormalization.NormalizeCurrency(y.Currency),
                            y.LimitAmount
                        })
                        .ToList()
                })
                .ToList(),
            Goals = goals
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    Name = CustomerInsightNormalization.NormalizeKey(x.Name),
                    Currency = CustomerInsightNormalization.NormalizeCurrency(x.Currency),
                    x.TargetAmount,
                    x.ProgressAmount,
                    x.TargetDate,
                    Status = CustomerInsightNormalization.NormalizeKey(x.Status),
                    x.FundingAccountId
                })
                .ToList()
        };

        var canonicalJson = JsonSerializer.Serialize(hashEnvelope, JsonOptions);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static IReadOnlyList<string> CollectCurrencies(
        IReadOnlyList<PersonalAccount> accounts,
        IReadOnlyList<NormalizedTransaction> transactions,
        IReadOnlyList<Bill> bills,
        IReadOnlyList<Subscription> subscriptions,
        IReadOnlyList<Budget> budgets,
        IReadOnlyList<Goal> goals)
    {
        return accounts.Select(x => CustomerInsightNormalization.NormalizeCurrency(x.Currency))
            .Concat(transactions.Select(x => x.Currency))
            .Concat(bills.Select(x => CustomerInsightNormalization.NormalizeCurrency(x.Currency)))
            .Concat(subscriptions.Select(x => CustomerInsightNormalization.NormalizeCurrency(x.Currency)))
            .Concat(budgets.SelectMany(x => x.Lines.Select(y => CustomerInsightNormalization.NormalizeCurrency(y.Currency))))
            .Concat(goals.Select(x => CustomerInsightNormalization.NormalizeCurrency(x.Currency)))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x)
            .ToList();
    }
}
