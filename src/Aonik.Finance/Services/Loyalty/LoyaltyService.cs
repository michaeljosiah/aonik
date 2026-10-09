using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Loyalty;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Loyalty;

/// <summary>Small ownership/reservation layer over the canonical immutable journal.</summary>
internal sealed partial class LoyaltyService(
    FinanceDbContext db, ITenantProvider tenantProvider, IClock clock,
    ITenantSettingStore settings, IJournalWriter journals) : ILoyaltyService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private Guid TenantId => tenantProvider.GetCurrentTenantId();

    public async Task<LoyaltyPolicy> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        var raw = await settings.GetTenantValueAsync(LoyaltySettings.Policy, TenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw)) return new();
        LoyaltyPolicy policy;
        try
        {
            if (raw.Length > 32768) throw new JsonException();
            policy = JsonSerializer.Deserialize<LoyaltyPolicy>(raw, Json) ?? throw new JsonException();
        }
        catch (JsonException) { throw new InvalidStateException("The loyalty policy is unavailable."); }
        if (!policy.Enabled) return new();
        if (string.IsNullOrWhiteSpace(policy.Version) || policy.Version.Length > 128 || policy.Version.Any(char.IsControl) || policy.Ledger is null)
            throw new InvalidStateException("The loyalty policy requires a version and existing ledger accounts.");
        await BindingAsync(policy.Ledger, cancellationToken);
        var canonical = policy with
        {
            EarnExcludedProductIds = NormalizeIds(policy.EarnExcludedProductIds),
            RedeemExcludedProductIds = NormalizeIds(policy.RedeemExcludedProductIds),
            EarnExcludedDiscountIds = NormalizeIds(policy.EarnExcludedDiscountIds),
            RedeemExcludedDiscountIds = NormalizeIds(policy.RedeemExcludedDiscountIds)
        };
        // The generic settings writer cannot enforce that an operator changes a version label.
        return canonical with { Version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(canonical)))) };
    }

    public async Task<LoyaltyBalance> GetBalanceAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        var account = await db.LoyaltyAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId && x.PartyId == partyId, cancellationToken);
        if (account is null) return new(0, 0, 0, 0, 0);
        var balance = await PostedPointsAsync(account.Id, cancellationToken);
        var reserved = await ReservedPointsAsync(account.Id, cancellationToken);
        return new(balance, reserved, Math.Max(0, checked(balance - reserved)), balance / 100m, account.HighestFivePoundMarkSeen);
    }

    public async Task<LoyaltyActivityPage> GetActivityAsync(Guid partyId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100 || ((long)page - 1) * pageSize > int.MaxValue)
            throw new InvalidStateException("Use a positive page and pageSize between 1 and 100.");
        var account = await db.LoyaltyAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId && x.PartyId == partyId, cancellationToken);
        if (account is null) return new([], 0, page, pageSize);
        var query = ActivityQuery(account.Id);
        var count = await query.CountAsync(cancellationToken);
        var ordered = query.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id);
        var skip = (page - 1) * pageSize;
        var balance = await PostedPointsAsync(account.Id, cancellationToken);
        var preceding = skip == 0 ? 0 : AsPoints(await ordered.Take(skip).SumAsync(x => x.SignedAmount, cancellationToken));
        var rows = await ordered.Skip(skip).Take(pageSize).ToListAsync(cancellationToken);
        var running = checked(balance - preceding);
        var result = new List<LoyaltyActivity>();
        foreach (var row in rows)
        {
            var points = AsPoints(row.SignedAmount);
            result.Add(new(row.Id, row.Kind, row.OccurredAtUtc, points, running, row.OrderId, row.SourceId, row.OriginalOperationId, row.Reason));
            running = checked(running - points);
        }
        return new(result, count, page, pageSize);
    }

    public async Task<LoyaltyBalance> MarkSeenAsync(Guid partyId, long mark, CancellationToken cancellationToken = default)
    {
        if (partyId == Guid.Empty || mark < 0 || mark > long.MaxValue / 500)
            throw new InvalidStateException("The milestone must be a nonnegative count of five-pound steps.");
        await InTransactionAsync(async ct =>
        {
            var account = await db.LoyaltyAccounts.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.PartyId == partyId, ct);
            if (account is not null && mark > account.HighestFivePoundMarkSeen)
            {
                account.HighestFivePoundMarkSeen = mark;
                Touch(account);
            }
            return true;
        }, cancellationToken);
        return await GetBalanceAsync(partyId, cancellationToken);
    }

    private IQueryable<ActivityRow> ActivityQuery(Guid accountId) =>
        from operation in db.LoyaltyOperations.AsNoTracking()
        where operation.TenantId == TenantId && operation.AccountId == accountId
        join line in db.JournalEntryLines.AsNoTracking().Where(x => x.TenantId == TenantId)
            on operation.JournalEntryLineId equals (Guid?)line.Id into legs
        from line in legs.DefaultIfEmpty()
        select new ActivityRow
        {
            Id = operation.Id, Kind = operation.Kind, SourceId = operation.SourceId, OrderId = operation.OrderId,
            OriginalOperationId = operation.OriginalOperationId, OccurredAtUtc = operation.OccurredAtUtc, Reason = operation.Reason,
            SignedAmount = line == null ? 0m : line.Direction == JournalDirections.Credit ? line.Amount : -line.Amount
        };

    private async Task<long> PostedPointsAsync(Guid accountId, CancellationToken ct)
    {
        var invalid = await db.LoyaltyOperations.AsNoTracking().Where(x => x.TenantId == TenantId && x.AccountId == accountId && x.JournalEntryLineId != null)
            .AnyAsync(operation => !db.JournalEntryLines.Any(line => line.TenantId == TenantId
                && line.Id == operation.JournalEntryLineId && line.JournalEntryId == operation.JournalEntryId
                && line.Currency == "GBP" && (line.Direction == JournalDirections.Debit || line.Direction == JournalDirections.Credit)
                && db.JournalEntries.Any(entry => entry.Id == line.JournalEntryId && entry.TenantId == TenantId && entry.Status == "Posted")), ct);
        if (invalid) throw new InvalidStateException("A loyalty journal reference is unavailable.");
        return AsPoints(await ActivityQuery(accountId).SumAsync(x => x.SignedAmount, ct));
    }

    private Task<long> ReservedPointsAsync(Guid accountId, CancellationToken ct) => db.LoyaltyCheckoutAttempts.AsNoTracking()
        .Where(x => x.TenantId == TenantId && x.AccountId == accountId && x.Status == "Reserved")
        .SumAsync(x => x.ReservedPoints, ct);

    private async Task<LoyaltyAccount> AccountAsync(Guid partyId, LoyaltyLedgerBinding binding, CancellationToken ct)
    {
        var account = db.LoyaltyAccounts.Local.SingleOrDefault(x => x.TenantId == TenantId && x.PartyId == partyId)
            ?? await db.LoyaltyAccounts.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.PartyId == partyId, ct);
        if (account is null)
        {
            if (partyId == Guid.Empty || !await db.Parties.AnyAsync(x => x.TenantId == TenantId && x.Id == partyId, ct))
                throw new InvalidStateException("The loyalty owner is unavailable.");
            account = new() { TenantId = TenantId, PartyId = partyId, LedgerId = binding.LedgerId, LiabilityAccountId = binding.LiabilityAccountId };
            db.LoyaltyAccounts.Add(account);
        }
        else if (account.LedgerId != binding.LedgerId || account.LiabilityAccountId != binding.LiabilityAccountId)
            throw new InvalidStateException("Existing loyalty liability cannot be moved by changing configuration.");
        return account;
    }

    private async Task<Dictionary<Guid, LedgerAccount>> BindingAsync(LoyaltyLedgerBinding binding, CancellationToken ct)
    {
        if (binding.LedgerId == Guid.Empty || binding.LiabilityAccountId == Guid.Empty || binding.EarnExpenseAccountId == Guid.Empty
            || binding.RedeemExpenseAccountId == Guid.Empty || binding.LiabilityAccountId == binding.EarnExpenseAccountId
            || binding.LiabilityAccountId == binding.RedeemExpenseAccountId)
            throw new InvalidStateException("Loyalty requires explicit existing ledger accounts.");
        if (!await db.Ledgers.AnyAsync(x => x.TenantId == TenantId && x.Id == binding.LedgerId && x.BaseCurrency == "GBP", ct))
            throw new InvalidStateException("The loyalty ledger must be an existing tenant GBP ledger.");
        var ids = new[] { binding.LiabilityAccountId, binding.EarnExpenseAccountId, binding.RedeemExpenseAccountId }.Distinct().ToArray();
        var accounts = await db.LedgerAccounts.AsNoTracking().Where(x => x.TenantId == TenantId && x.LedgerId == binding.LedgerId && ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        if (accounts.Count != ids.Length || accounts[binding.LiabilityAccountId].AccountType != "Liability"
            || accounts[binding.EarnExpenseAccountId].AccountType != "Expense" || accounts[binding.RedeemExpenseAccountId].AccountType != "Expense")
            throw new InvalidStateException("Loyalty liability and expense accounts are unavailable or have incompatible types.");
        return accounts;
    }

    private async Task<LoyaltyOperation> PostAsync(LoyaltyAccount account, string kind, Guid sourceId, long points,
        LoyaltyLedgerBinding binding, Guid offsetId, Guid? orderId, Guid? originalId, string details, string? reason, CancellationToken ct)
    {
        var existing = await OperationAsync(kind, sourceId, ct);
        if (existing is not null)
        {
            if (existing.AccountId != account.Id || existing.Points != points || existing.OrderId != orderId
                || existing.OriginalOperationId != originalId || existing.DetailsJson != details || existing.Reason != reason)
                throw new InvalidStateException("The loyalty source was already recorded with different facts.");
            return existing;
        }
        var operation = NewOperation(account, kind, sourceId, points, orderId, originalId, details, reason);
        Touch(account);
        if (points != 0)
        {
            var accounts = await BindingAsync(binding, ct);
            var amount = Math.Abs(points / 100m);
            var direction = points > 0 ? JournalDirections.Credit : JournalDirections.Debit;
            var dimensions = Serialize(new { loyaltyAccountId = account.Id, operationId = operation.Id, orderId });
            var journal = await journals.PostAsync(new(binding.LedgerId, "Loyalty" + kind, sourceId,
            [
                new(accounts[binding.LiabilityAccountId].Code, direction, amount, "GBP", reason, dimensions),
                new(accounts[offsetId].Code, points > 0 ? JournalDirections.Debit : JournalDirections.Credit, amount, "GBP", reason)
            ], clock.UtcNow), ct);
            operation.JournalEntryId = journal.JournalEntryId;
            operation.JournalEntryLineId = await FindLegAsync(journal.JournalEntryId, binding, direction, amount, ct);
        }
        db.LoyaltyOperations.Add(operation);
        return operation;
    }

    private Task<Guid> FindLegAsync(Guid entryId, LoyaltyLedgerBinding binding, string direction, decimal amount, CancellationToken ct) =>
        db.JournalEntryLines.Where(x => x.TenantId == TenantId && x.JournalEntryId == entryId
            && x.LedgerAccountId == binding.LiabilityAccountId && x.Direction == direction && x.Currency == "GBP" && x.Amount == amount)
            .Select(x => x.Id).SingleAsync(ct);

    private async Task<LoyaltyOperation?> OperationAsync(string kind, Guid sourceId, CancellationToken ct) =>
        db.LoyaltyOperations.Local.SingleOrDefault(x => x.TenantId == TenantId && x.Kind == kind && x.SourceId == sourceId)
        ?? await db.LoyaltyOperations.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Kind == kind && x.SourceId == sourceId, ct);

    private LoyaltyOperation NewOperation(LoyaltyAccount account, string kind, Guid sourceId, long points,
        Guid? orderId, Guid? originalId, string details, string? reason = null) => new()
    {
        TenantId = TenantId, AccountId = account.Id, Kind = kind, SourceId = sourceId, Points = points,
        OrderId = orderId, OriginalOperationId = originalId, DetailsJson = details, Reason = reason, OccurredAtUtc = clock.UtcNow
    };

    private void Touch(LoyaltyAccount account)
    {
        account.UpdatedAt = clock.UtcNow;
        if (db.Entry(account).State != EntityState.Added) db.Entry(account).Property(x => x.UpdatedAt).IsModified = true;
    }

    private async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
                {
                    Detach();
                    await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(token) : null;
                    var result = await action(token);
                    await db.SaveChangesAsync(token);
                    if (transaction is not null) await transaction.CommitAsync(token);
                    return result;
                }, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < 2) { Detach(); }
            catch (DbUpdateException error) when (attempt < 2 && error.InnerException is SqlException { Number: 2601 or 2627 }) { Detach(); }
            catch { Detach(); throw; }
        }
    }

    private void Detach()
    {
        foreach (var entry in db.ChangeTracker.Entries().Where(x => x.Entity is LoyaltyAccount or LoyaltyOperation or LoyaltyCheckoutAttempt or JournalEntry or JournalEntryLine).ToArray())
            entry.State = EntityState.Detached;
    }

    private static long AsPoints(decimal amount)
    {
        var points = amount * 100m;
        if (points != decimal.Truncate(points)) throw new InvalidStateException("The loyalty journal contains a fractional point.");
        return checked((long)points);
    }

    private static Guid[] NormalizeIds(IReadOnlyList<Guid>? ids)
    {
        if (ids is null) return [];
        if (ids.Count > 200 || ids.Contains(Guid.Empty) || ids.Distinct().Count() != ids.Count)
            throw new InvalidStateException("Loyalty exclusions require at most 200 distinct valid identifiers.");
        return ids.Order().ToArray();
    }

    private static string Serialize<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, Json);
        if (json.Length > 262144) throw new InvalidStateException("The loyalty snapshot is too large.");
        return json;
    }

    private static LoyaltyCheckout Read(LoyaltyCheckoutAttempt attempt) => JsonSerializer.Deserialize<LoyaltyCheckout>(attempt.SnapshotJson, Json)
        ?? throw new InvalidStateException("The frozen loyalty instruction is unavailable.");

    private sealed class ActivityRow
    {
        public Guid Id { get; set; }
        public string Kind { get; set; } = "";
        public Guid SourceId { get; set; }
        public Guid? OrderId { get; set; }
        public Guid? OriginalOperationId { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string? Reason { get; set; }
        public decimal SignedAmount { get; set; }
    }
}
