using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Partners.Connectors.Credentials;
using Aonik.Finance.Services.Partners.Connectors.Registry;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Finance.Services.Payments;

internal sealed class StripeConnectorResolver(
    FinanceDbContext dbContext,
    ITenantProvider tenantProvider,
    ITenantSettingStore settings,
    ICredentialBundleService credentials,
    IClock clock) : IStripeConnectorResolver
{
    public async Task<StripeConnectorBinding> ResolveSelectedAsync(CancellationToken cancellationToken = default)
    {
        var value = await settings.GetTenantValueAsync(StripeSettingNames.ConnectorId,
            tenantProvider.GetCurrentTenantId(), cancellationToken);
        if (!Guid.TryParse(value, out var connectorId) || connectorId == Guid.Empty)
        {
            throw new InvalidOperationException("Stripe checkout is not configured for this tenant.");
        }

        return await ResolveAsync(connectorId, requireActive: true, cancellationToken);
    }

    public Task<StripeConnectorBinding> ResolveBoundAsync(Guid connectorId, CancellationToken cancellationToken = default)
        => ResolveAsync(connectorId, requireActive: false, cancellationToken);

    private async Task<StripeConnectorBinding> ResolveAsync(
        Guid connectorId, bool requireActive, CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var connector = await dbContext.Connectors.AsNoTracking().FirstOrDefaultAsync(
            c => c.Id == connectorId && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
        if (connector is null
            || !string.Equals(connector.ConnectorType, ConnectorRegistry.StripeCheckoutV1, StringComparison.OrdinalIgnoreCase)
            || (requireActive && !string.Equals(connector.Status, "Active", StringComparison.OrdinalIgnoreCase))
            || string.IsNullOrWhiteSpace(connector.CredentialsRef))
        {
            throw new InvalidOperationException("The tenant's Stripe connector is unavailable.");
        }

        ConnectorConfigJson.Validate(ConnectorRegistry.GetRequired(ConnectorRegistry.StripeCheckoutV1), connector.ConfigJson);
        var config = ConnectorConfigJson.Parse(connector.ConfigJson);
        var bundle = await credentials.ResolveAsync(connector.CredentialsRef, cancellationToken);
        if (bundle is null || !string.Equals(bundle.ConnectorKind, ConnectorRegistry.StripeCheckoutV1, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The tenant's Stripe credentials are unavailable.");
        }

        var secret = bundle.Secrets.GetCurrent(ConnectorRegistry.FieldSecretKey);
        var signingSecrets = bundle.Secrets.GetVerificationCandidates(ConnectorRegistry.FieldSigningSecret, clock.UtcNow);
        var liveMode = string.Equals(config[ConnectorRegistry.ConfigEnvironment], ConnectorRegistry.EnvironmentProduction, StringComparison.OrdinalIgnoreCase);
        var prefix = liveMode ? "sk_live_" : "sk_test_";
        var restrictedPrefix = liveMode ? "rk_live_" : "rk_test_";
        if (string.IsNullOrWhiteSpace(secret)
            || !(secret.StartsWith(prefix, StringComparison.Ordinal) || secret.StartsWith(restrictedPrefix, StringComparison.Ordinal))
            || secret.Length <= prefix.Length || secret.Any(char.IsWhiteSpace)
            || !bundle.Secrets.Has(ConnectorRegistry.FieldSigningSecret)
            || signingSecrets.Count == 0 || signingSecrets.Any(s => s.Length <= 6 || !s.StartsWith("whsec_", StringComparison.Ordinal) || s.Any(char.IsWhiteSpace)))
        {
            throw new InvalidOperationException("Stripe credentials do not match the connector environment or required fields.");
        }

        return new StripeConnectorBinding
        {
            TenantId = tenantId, ConnectorId = connector.Id,
            ProviderAccountId = config[ConnectorRegistry.ConfigAccountId], LiveMode = liveMode,
            ReturnOrigin = new Uri(config[ConnectorRegistry.ConfigReturnOrigin]).GetLeftPart(UriPartial.Authority),
            SecretKey = secret, SigningSecrets = signingSecrets,
        };
    }
}
