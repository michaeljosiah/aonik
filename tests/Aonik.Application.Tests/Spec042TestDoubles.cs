using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Services.Partners.Connectors;
using Aonik.Finance.Services.Partners.Connectors.Credentials;

// Spec 042 test doubles in the global namespace so every test file can use them without a using directive.
// Existing service tests exercise the Simulated/legacy path (no persisted connector rows or bundles), so the
// factory is never invoked and the bundle service resolves to "no bundle" (legacy fallback).

internal sealed class Spec042NoopConnectorFactory : IPartnerConnectorFactory
{
    public IPartnerPayoutConnector CreatePayout(Connector row) =>
        throw new NotSupportedException("The connector factory is not exercised by this test.");

    public IPartnerBillPaymentConnector CreateBillPayment(Connector row) =>
        throw new NotSupportedException("The connector factory is not exercised by this test.");
}

// IPartnerConnectorFactory is internal, so Moq cannot proxy it; this stub returns the supplied connectors.
internal sealed class Spec042StubConnectorFactory(
    IPartnerBillPaymentConnector? billConnector = null,
    IPartnerPayoutConnector? payoutConnector = null) : IPartnerConnectorFactory
{
    public IPartnerPayoutConnector CreatePayout(Connector row) =>
        payoutConnector ?? throw new NotSupportedException("No payout connector configured for this stub.");

    public IPartnerBillPaymentConnector CreateBillPayment(Connector row) =>
        billConnector ?? throw new NotSupportedException("No bill connector configured for this stub.");
}

internal sealed class Spec042NoopCredentialBundleService : ICredentialBundleService
{
    public Task<ResolvedCredentialBundle?> ResolveAsync(string credentialsRef, CancellationToken cancellationToken = default)
        => Task.FromResult<ResolvedCredentialBundle?>(null);

    public Task<CredentialBundle> UpsertAsync(CredentialBundleWriteRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Bundle writes are not exercised by this test.");

    public Task<bool> RotateFieldAsync(
        string credentialsRef, string field, string newValue, TimeSpan? previousTtl = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Bundle rotation is not exercised by this test.");

    public Task<IReadOnlyList<CredentialFieldState>> GetFieldStatesAsync(
        string credentialsRef, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Bundle field state is not exercised by this test.");
}
