using System.Diagnostics;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class CustomerInsightSnapshotService : ICustomerInsightSnapshotService
{
    private readonly FinanceDbContext _dbContext;
    private readonly ICustomerInsightSnapshotGenerator _generator;
    private readonly ICustomerInsightSnapshotReader _reader;
    private readonly IClock _clock;

    public CustomerInsightSnapshotService(
        FinanceDbContext dbContext,
        ICustomerInsightSnapshotGenerator generator,
        ICustomerInsightSnapshotReader reader,
        IClock clock)
    {
        _dbContext = dbContext;
        _generator = generator;
        _reader = reader;
        _clock = clock;
    }

    public async Task<CustomerInsightSnapshotResponse> GenerateCurrentSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var current = await _dbContext.CustomerInsightSnapshots
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.Status == CustomerInsightSnapshotContract.StatusCurrent,
                cancellationToken);

        try
        {
            var generated = await _generator.GenerateAsync(userId, cancellationToken);

            if (current is not null
                && current.SourceHash == generated.SourceHash
                && current.WindowStartUtc == generated.WindowStartUtc
                && current.WindowEndUtc == generated.WindowEndUtc
                && current.GeneratedBy == generated.GeneratedBy)
            {
                return await _reader.GetSnapshotAsync(current.Id, cancellationToken)
                    ?? throw new InvalidOperationException($"Customer insight snapshot {current.Id} was not found after generation.");
            }

            var nextVersion = (current?.Version ?? 0) + 1;
            var snapshot = new CustomerInsightSnapshot
            {
                TenantId = generated.Snapshot.TenantId,
                UserId = userId,
                Status = CustomerInsightSnapshotContract.StatusCurrent,
                AsOfUtc = generated.AsOfUtc,
                WindowStartUtc = generated.WindowStartUtc,
                WindowEndUtc = generated.WindowEndUtc,
                Version = nextVersion,
                SourceHash = generated.SourceHash,
                SnapshotJson = generated.SnapshotJson,
                GeneratedBy = generated.GeneratedBy,
                GenerationDurationMs = (int)stopwatch.ElapsedMilliseconds,
            };

            _dbContext.CustomerInsightSnapshots.Add(snapshot);

            if (current is not null)
            {
                current.Status = CustomerInsightSnapshotContract.StatusSuperseded;
                current.SupersededById = snapshot.Id;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return await _reader.GetSnapshotAsync(snapshot.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Customer insight snapshot {snapshot.Id} was not found after persistence.");
        }
        catch (Exception ex)
        {
            var failedSnapshot = new CustomerInsightSnapshot
            {
                UserId = userId,
                Status = CustomerInsightSnapshotContract.StatusFailed,
                AsOfUtc = _clock.UtcNow,
                WindowStartUtc = ResolveBehaviourWindowStart(_clock.UtcNow),
                WindowEndUtc = ResolveWindowEnd(_clock.UtcNow),
                Version = (current?.Version ?? 0) + 1,
                SourceHash = string.Empty,
                SnapshotJson = string.Empty,
                GeneratedBy = CustomerInsightSnapshotContract.GeneratorVersion,
                GenerationDurationMs = (int)stopwatch.ElapsedMilliseconds,
                FailureReason = TruncateFailureReason(ex.Message)
            };

            _dbContext.CustomerInsightSnapshots.Add(failedSnapshot);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new CustomerInsightSnapshotResponse(
                failedSnapshot.Id,
                failedSnapshot.UserId,
                failedSnapshot.Status,
                failedSnapshot.AsOfUtc,
                failedSnapshot.WindowStartUtc,
                failedSnapshot.WindowEndUtc,
                failedSnapshot.Version,
                failedSnapshot.SourceHash,
                failedSnapshot.GeneratedBy,
                failedSnapshot.GenerationDurationMs,
                failedSnapshot.FailureReason,
                failedSnapshot.SupersededById,
                failedSnapshot.CreatedAt,
                failedSnapshot.UpdatedAt,
                null);
        }
    }

    private static DateTime ResolveWindowEnd(DateTime nowUtc) =>
        nowUtc.Date.AddDays(1).AddTicks(-1);

    private static DateTime ResolveBehaviourWindowStart(DateTime nowUtc) =>
        nowUtc.Date.AddDays(-(CustomerInsightSnapshotContract.BehaviourWindowDays - 1));

    private static string TruncateFailureReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "Unknown snapshot generation error.";
        }

        return reason.Length <= 1000 ? reason : reason[..1000];
    }
}
