using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance;

/// <summary>
/// Adapts the rich <see cref="ICustomerInsightSnapshotReader"/> output to
/// the AI-shaped <see cref="ICustomerInsightSnapshotForAi"/> contract.
/// Lets <c>Aonik.Ai</c> consume snapshots through SharedKernel without
/// taking a back-pointing reference on the Finance contracts.
/// </summary>
internal sealed class CustomerInsightSnapshotForAiAdapter : ICustomerInsightSnapshotForAi
{
    /// <summary>
    /// Snapshot serialiser settings. Match the camelCase + null-omit
    /// convention used by the Ai summariser's prior inline serialisation
    /// so prompt content stays byte-for-byte identical post-refactor.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ICustomerInsightSnapshotReader _reader;

    public CustomerInsightSnapshotForAiAdapter(ICustomerInsightSnapshotReader reader)
    {
        _reader = reader;
    }

    public async Task<CustomerInsightSnapshotForAi?> GetSnapshotForSummaryAsync(
        Guid snapshotId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _reader.GetSnapshotAsync(snapshotId, cancellationToken);
        if (snapshot?.Snapshot is null)
        {
            return null;
        }

        var snapshotJson = JsonSerializer.Serialize(snapshot.Snapshot, JsonOptions);

        return new CustomerInsightSnapshotForAi(
            Id: snapshot.Id,
            TenantId: snapshot.Snapshot.TenantId,
            UserId: snapshot.UserId,
            AsOfUtc: snapshot.AsOfUtc,
            WindowStartUtc: snapshot.WindowStartUtc,
            WindowEndUtc: snapshot.WindowEndUtc,
            Version: snapshot.Version,
            SnapshotJson: snapshotJson);
    }
}
