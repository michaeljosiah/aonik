using System.Text.Json;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Services.PersonalFinance;

/// <summary>
/// Materializes secondary, user-linked behavioural insight rows from the current
/// canonical customer insight snapshot. These generic Insight records are retained
/// only for secondary/admin surfaces and legacy fallback readers.
/// </summary>
internal sealed class BehaviouralInsightGenerator
{
    private const string SubjectType = "UserBehaviour";
    private const int MaxSignals = 5;

    private readonly ICustomerInsightSnapshotReader _snapshotReader;
    private readonly IInsightReader _insightReader;
    private readonly IInsightWriter _insightWriter;
    private readonly ILogger<BehaviouralInsightGenerator> _logger;

    public BehaviouralInsightGenerator(
        ICustomerInsightSnapshotReader snapshotReader,
        IInsightReader insightReader,
        IInsightWriter insightWriter,
        ILogger<BehaviouralInsightGenerator> logger)
    {
        _snapshotReader = snapshotReader;
        _insightReader = insightReader;
        _insightWriter = insightWriter;
        _logger = logger;
    }

    public async Task GenerateAllForUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var currentSnapshot = await _snapshotReader.GetCurrentSnapshotAsync(userId, cancellationToken);
        if (currentSnapshot?.Snapshot is null)
        {
            _logger.LogDebug(
                "Skipping secondary behavioural insight generation for user {UserId} in tenant {TenantId} because no current snapshot exists.",
                userId,
                tenantId);
            return;
        }

        var existingInsights = await _insightReader.ListBySubjectAsync(SubjectType, userId, cancellationToken);
        if (existingInsights.Any(x => x.CreatedUtc >= currentSnapshot.CreatedAt))
        {
            _logger.LogDebug(
                "Skipping secondary behavioural insight generation for user {UserId} in tenant {TenantId} because current snapshot {SnapshotId} is already reflected in generic insights.",
                userId,
                tenantId,
                currentSnapshot.Id);
            return;
        }

        var signals = currentSnapshot.Snapshot.Signals
            .OrderByDescending(x => SeverityRank(x.Severity))
            .ThenBy(x => x.Category)
            .ThenBy(x => x.SignalKey)
            .Take(MaxSignals)
            .ToList();

        foreach (var signal in signals)
        {
            var metadataJson = JsonSerializer.Serialize(new
            {
                source = "customer_insight_snapshot",
                customerInsightSnapshotId = currentSnapshot.Id,
                signal.SignalKey,
                signal.Category,
                signal.Severity,
                signal.Confidence,
                signal.MetricRefs,
                signal.WindowStartUtc,
                signal.WindowEndUtc
            });

            await _insightWriter.SaveInsightAsync(
                SubjectType,
                userId,
                signal.Title,
                signal.Description,
                metadataJson,
                userId,
                currentSnapshot.AsOfUtc.AddDays(30),
                cancellationToken);
        }
    }

    private static int SeverityRank(string severity) => severity switch
    {
        CustomerInsightSnapshotContract.SeverityCritical => 4,
        CustomerInsightSnapshotContract.SeverityHigh => 3,
        CustomerInsightSnapshotContract.SeverityModerate => 2,
        _ => 1
    };
}
