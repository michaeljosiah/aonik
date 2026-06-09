using Aonik.Finance.Services.Partners.Connectors;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave;

internal interface IFlutterwaveConfigProvider
{
    /// <summary>
    /// Resolves the legacy-default / global options from the <c>Finance.Partners.Flutterwave.*</c> settings.
    /// Used only for the migrated default connector and any unbound back-compat path (Spec 042 §7.2).
    /// </summary>
    Task<FlutterwaveOptions> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves options for a specific connector row (Spec 042 §8). When the binding carries a
    /// <c>CredentialsRef</c>, secrets come from the decrypted bundle and transport endpoints are derived from
    /// the <c>environment</c> config; otherwise the legacy keys apply <strong>only</strong> if the binding
    /// allows the fail-closed fallback, else the call throws.
    /// </summary>
    Task<FlutterwaveOptions> GetAsync(ConnectorBinding binding, CancellationToken cancellationToken = default);
}
