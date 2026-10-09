namespace Aonik.Finance.Contracts.Services.Payments;

public interface IStripeConnectorResolver
{
    Task<StripeConnectorBinding> ResolveSelectedAsync(CancellationToken cancellationToken = default);

    Task<StripeConnectorBinding> ResolveBoundAsync(Guid connectorId, CancellationToken cancellationToken = default);
}

/// <summary>Transient server-only binding. Credentials must never be logged or serialized.</summary>
public sealed class StripeConnectorBinding
{
    public required Guid TenantId { get; init; }
    public required Guid ConnectorId { get; init; }
    public required string ProviderAccountId { get; init; }
    public required bool LiveMode { get; init; }
    public required string ReturnOrigin { get; init; }
    public required string SecretKey { get; init; }
    public required IReadOnlyList<string> SigningSecrets { get; init; }
}
