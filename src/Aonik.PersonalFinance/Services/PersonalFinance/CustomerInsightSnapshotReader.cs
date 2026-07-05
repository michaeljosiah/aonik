using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.PersonalFinance.Services;

internal sealed class CustomerInsightSnapshotReader : ICustomerInsightSnapshotReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly PersonalFinanceDbContext _dbContext;

    public CustomerInsightSnapshotReader(PersonalFinanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CustomerInsightSnapshotResponse?> GetCurrentSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _dbContext.CustomerInsightSnapshots
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Status == CustomerInsightSnapshotContract.StatusCurrent)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return snapshot is null ? null : Map(snapshot);
    }

    public async Task<CustomerInsightSnapshotResponse?> GetSnapshotAsync(
        Guid snapshotId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _dbContext.CustomerInsightSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == snapshotId, cancellationToken);

        return snapshot is null ? null : Map(snapshot);
    }

    public async Task<IReadOnlyList<CustomerInsightSnapshotHistoryItemResponse>> GetSnapshotHistoryAsync(
        Guid userId,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var boundedTake = take <= 0 ? 20 : Math.Min(take, 100);

        var snapshots = await _dbContext.CustomerInsightSnapshots
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Version)
            .Take(boundedTake)
            .ToListAsync(cancellationToken);

        return snapshots.Select(MapHistory).ToList();
    }

    private static CustomerInsightSnapshotResponse Map(CustomerInsightSnapshot snapshot)
    {
        CustomerInsightSnapshotDocument? document = null;

        if (!string.IsNullOrWhiteSpace(snapshot.SnapshotJson))
        {
            document = JsonSerializer.Deserialize<CustomerInsightSnapshotDocument>(snapshot.SnapshotJson, JsonOptions);
        }

        return new CustomerInsightSnapshotResponse(
            snapshot.Id,
            snapshot.UserId,
            snapshot.Status,
            snapshot.AsOfUtc,
            snapshot.WindowStartUtc,
            snapshot.WindowEndUtc,
            snapshot.Version,
            snapshot.SourceHash,
            snapshot.GeneratedBy,
            snapshot.GenerationDurationMs,
            snapshot.FailureReason,
            snapshot.SupersededById,
            snapshot.CreatedAt,
            snapshot.UpdatedAt,
            document);
    }

    private static CustomerInsightSnapshotHistoryItemResponse MapHistory(CustomerInsightSnapshot snapshot)
    {
        var isPartial = false;

        if (!string.IsNullOrWhiteSpace(snapshot.SnapshotJson))
        {
            var document = JsonSerializer.Deserialize<CustomerInsightSnapshotDocument>(snapshot.SnapshotJson, JsonOptions);
            isPartial = document?.Coverage.IsPartial ?? false;
        }

        return new CustomerInsightSnapshotHistoryItemResponse(
            snapshot.Id,
            snapshot.Status,
            snapshot.AsOfUtc,
            snapshot.WindowStartUtc,
            snapshot.WindowEndUtc,
            snapshot.Version,
            snapshot.SourceHash,
            snapshot.GeneratedBy,
            snapshot.GenerationDurationMs,
            snapshot.FailureReason,
            snapshot.SupersededById,
            isPartial,
            snapshot.CreatedAt,
            snapshot.UpdatedAt);
    }
}
