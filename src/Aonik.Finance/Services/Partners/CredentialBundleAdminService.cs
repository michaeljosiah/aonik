using System.Text.Json;

using Aonik.Finance.Contracts.Models.Partners;
using Aonik.Finance.Contracts.Services.Partners;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Partners.Connectors;
using Aonik.Finance.Services.Partners.Connectors.Credentials;
using Aonik.Finance.Services.Partners.Connectors.Registry;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Partners;

/// <summary>
/// Admin orchestration over <see cref="ICredentialBundleService"/> + the connector registry (Spec 042 §12).
/// Maps internal types to value-free DTOs, enforces admin permission, and implements the idempotent
/// legacy-config lift (§13). Secret values never leave this boundary.
/// </summary>
internal sealed class CredentialBundleAdminService : FinanceServiceBase, ICredentialBundleAdminService
{
    private const string FlutterwavePartnerName = "Flutterwave";

    private readonly FinanceDbContext _dbContext;
    private readonly ICredentialBundleService _bundleService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ISettingProvider _settingProvider;
    private readonly IClock _clock;

    public CredentialBundleAdminService(
        FinanceDbContext dbContext,
        ICredentialBundleService bundleService,
        ITenantProvider tenantProvider,
        ISettingProvider settingProvider,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        IPermissionService permissionService)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _bundleService = bundleService;
        _tenantProvider = tenantProvider;
        _settingProvider = settingProvider;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ConnectorKindSchemaDto>> GetConnectorKindsAsync(CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Settings.Write", cancellationToken);
        return ConnectorRegistry.All.Select(MapKind).ToList();
    }

    public async Task<IReadOnlyList<CredentialBundleListItem>> ListBundlesAsync(CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Settings.Write", cancellationToken);

        var bundles = await _dbContext.CredentialBundles.AsNoTracking().ToListAsync(cancellationToken);
        var refs = bundles.Select(b => b.Ref).ToList();

        // Which connectors bind each bundle (CredentialsRef stores the bundle Ref).
        var bindings = await _dbContext.Connectors.AsNoTracking()
            .Where(c => c.CredentialsRef != null && refs.Contains(c.CredentialsRef))
            .Select(c => new { c.Id, c.CredentialsRef })
            .ToListAsync(cancellationToken);

        return bundles.Select(bundle =>
        {
            var bound = bindings
                .Where(b => string.Equals(b.CredentialsRef, bundle.Ref, StringComparison.OrdinalIgnoreCase))
                .Select(b => b.Id)
                .ToList();
            return MapBundle(bundle, bound);
        }).ToList();
    }

    public async Task<CredentialBundleListItem> CreateBundleAsync(
        CreateCredentialBundleRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Settings.Write", cancellationToken);

        // Create must NOT silently update an existing ref: a re-used ref could change the ConnectorKind /
        // secrets of a bundle that connectors already bind to, leaving them pointed at an incompatible bundle.
        // Reject the ref here and route mutations through UpdateBundleAsync (PATCH); the unique (TenantId, Ref)
        // index is the hard backstop against a concurrent create.
        var normalizedRef = request.Ref?.Trim() ?? string.Empty;
        var exists = await _dbContext.CredentialBundles.AsNoTracking()
            .AnyAsync(b => b.Ref == normalizedRef, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException(
                $"A credential bundle with ref '{normalizedRef}' already exists; update it via PATCH instead.");
        }

        var bundle = await _bundleService.UpsertAsync(
            new CredentialBundleWriteRequest(request.Ref, request.Name, request.ConnectorKind, request.Secrets),
            cancellationToken);
        return await LoadListItemAsync(bundle.Ref, cancellationToken);
    }

    public async Task<CredentialBundleListItem> UpdateBundleAsync(
        string bundleRef, UpdateCredentialBundleRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Settings.Write", cancellationToken);

        var existing = await _dbContext.CredentialBundles.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Ref == bundleRef, cancellationToken)
            ?? throw new ArgumentException($"Credential bundle '{bundleRef}' was not found.", nameof(bundleRef));

        var bundle = await _bundleService.UpsertAsync(
            new CredentialBundleWriteRequest(
                bundleRef, request.Name ?? existing.Name, existing.ConnectorKind, request.Secrets),
            cancellationToken);
        return await LoadListItemAsync(bundle.Ref, cancellationToken);
    }

    public async Task<CredentialBundleListItem> RotateFieldAsync(
        string bundleRef, RotateCredentialFieldRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Settings.Write", cancellationToken);

        var ttl = request.PreviousTtlHours is > 0 ? TimeSpan.FromHours(request.PreviousTtlHours.Value) : (TimeSpan?)null;
        var rotated = await _bundleService.RotateFieldAsync(bundleRef, request.Field, request.NewValue, ttl, cancellationToken);
        if (!rotated)
        {
            throw new ArgumentException($"Credential bundle '{bundleRef}' was not found.", nameof(bundleRef));
        }

        return await LoadListItemAsync(bundleRef, cancellationToken);
    }

    public async Task<LiftLegacyFlutterwaveResult> LiftLegacyFlutterwaveAsync(CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Settings.Write", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var partner = await _dbContext.Partners
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Name == FlutterwavePartnerName, cancellationToken);
        if (partner is null)
        {
            partner = new Partner
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = FlutterwavePartnerName,
                Status = "Active",
                CapabilitiesJson = "{}",
                OperatingHoursJson = "{}",
                CreatedAt = _clock.UtcNow,
            };
            _dbContext.Partners.Add(partner);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Read the current provider-singleton values (ISettingProvider decrypts encrypted keys). They are
        // re-encrypted into bundles via IDataProtection — plaintext is never persisted (Spec 042 §13).
        var baseUrl = await _settingProvider.GetAsync(PartnerGatewaySettingNames.FlutterwaveBaseUrl, cancellationToken);
        var environment = baseUrl is not null && baseUrl.Contains("sandbox", StringComparison.OrdinalIgnoreCase)
            ? ConnectorRegistry.EnvironmentSandbox
            : baseUrl is null
                ? ConnectorRegistry.EnvironmentSandbox
                : ConnectorRegistry.EnvironmentProduction;

        var refs = new List<string>();

        var payoutSecrets = await CollectAsync(cancellationToken,
            (ConnectorRegistry.FieldClientId, PartnerGatewaySettingNames.FlutterwaveClientId),
            (ConnectorRegistry.FieldClientSecret, PartnerGatewaySettingNames.FlutterwaveClientSecret),
            (ConnectorRegistry.FieldEncryptionKey, PartnerGatewaySettingNames.FlutterwaveEncryptionKey),
            (ConnectorRegistry.FieldSigningSecret, PartnerGatewaySettingNames.FlutterwaveSigningSecret));
        string? payoutRef = null;
        if (payoutSecrets.Count > 0)
        {
            payoutRef = "fw-default-payout";
            await _bundleService.UpsertAsync(new CredentialBundleWriteRequest(
                payoutRef, "Flutterwave payout (default)", ConnectorRegistry.FlutterwavePayoutV4, payoutSecrets), cancellationToken);
            refs.Add(payoutRef);
        }

        var billsSecrets = await CollectAsync(cancellationToken,
            (ConnectorRegistry.FieldSecretKey, PartnerGatewaySettingNames.FlutterwaveBillsSecretKey));
        string? billsRef = null;
        if (billsSecrets.Count > 0)
        {
            billsRef = "fw-default-bills";
            await _bundleService.UpsertAsync(new CredentialBundleWriteRequest(
                billsRef, "Flutterwave bills (default)", ConnectorRegistry.FlutterwaveBillsV3, billsSecrets), cancellationToken);
            refs.Add(billsRef);
        }

        var payoutConnector = await EnsureDefaultConnectorAsync(
            tenantId, partner.Id, ConnectorRegistry.FlutterwavePayoutV4, payoutRef, environment, cancellationToken);
        var billsConnector = await EnsureDefaultConnectorAsync(
            tenantId, partner.Id, ConnectorRegistry.FlutterwaveBillsV3, billsRef, environment, cancellationToken);

        // Backfill ConnectorId on existing money records that predate binding (Guid.Empty → default payout).
        var payouts = await _dbContext.Payouts.Where(p => p.ConnectorId == Guid.Empty).ToListAsync(cancellationToken);
        foreach (var payout in payouts)
        {
            payout.ConnectorId = payoutConnector.Id;
        }

        var transmissions = await _dbContext.Transmissions.Where(t => t.ConnectorId == Guid.Empty).ToListAsync(cancellationToken);
        foreach (var transmission in transmissions)
        {
            transmission.ConnectorId = payoutConnector.Id;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LiftLegacyFlutterwaveResult(
            partner.Id, refs, new[] { payoutConnector.Id, billsConnector.Id }, payouts.Count, transmissions.Count);
    }

    private async Task<Dictionary<string, string>> CollectAsync(
        CancellationToken cancellationToken, params (string Field, string SettingKey)[] map)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (field, key) in map)
        {
            var value = await _settingProvider.GetAsync(key, cancellationToken);
            if (!string.IsNullOrWhiteSpace(value))
            {
                result[field] = value.Trim();
            }
        }

        return result;
    }

    private async Task<Connector> EnsureDefaultConnectorAsync(
        Guid tenantId, Guid partnerId, string kind, string? credentialsRef, string environment,
        CancellationToken cancellationToken)
    {
        var descriptor = ConnectorRegistry.GetRequired(kind);
        var configJson = JsonSerializer.Serialize(BuildDefaultConfig(descriptor, environment));

        var connector = await _dbContext.Connectors
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.PartnerId == partnerId && c.ConnectorType == kind, cancellationToken);
        if (connector is null)
        {
            connector = new Connector
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PartnerId = partnerId,
                ConnectorType = kind,
                Status = "Active",
                IsLegacyDefault = true,
                CredentialsRef = credentialsRef,
                ConfigJson = configJson,
                CreatedAt = _clock.UtcNow,
            };
            _dbContext.Connectors.Add(connector);
        }
        else
        {
            connector.IsLegacyDefault = true;
            if (credentialsRef is not null)
            {
                connector.CredentialsRef = credentialsRef;
            }

            connector.ConfigJson = configJson;
            connector.UpdatedAt = _clock.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return connector;
    }

    private static Dictionary<string, string> BuildDefaultConfig(ConnectorKindDescriptor descriptor, string environment)
    {
        var config = new Dictionary<string, string> { [ConnectorRegistry.ConfigEnvironment] = environment };
        foreach (var field in descriptor.ConfigFields)
        {
            if (!string.Equals(field.Name, ConnectorRegistry.ConfigEnvironment, StringComparison.OrdinalIgnoreCase)
                && field.DefaultValue is not null)
            {
                config[field.Name] = field.DefaultValue;
            }
        }

        return config;
    }

    private async Task<CredentialBundleListItem> LoadListItemAsync(string bundleRef, CancellationToken cancellationToken)
    {
        var bundle = await _dbContext.CredentialBundles.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Ref == bundleRef, cancellationToken)
            ?? throw new InvalidOperationException($"Credential bundle '{bundleRef}' was not found after write.");

        var bound = await _dbContext.Connectors.AsNoTracking()
            .Where(c => c.CredentialsRef == bundleRef)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        return MapBundle(bundle, bound);
    }

    private static CredentialBundleListItem MapBundle(CredentialBundle bundle, IReadOnlyList<Guid> boundConnectorIds)
    {
        var states = CredentialFieldMetadata.Deserialize(bundle.FieldMetadataJson)
            .ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        var descriptor = ConnectorRegistry.Get(bundle.ConnectorKind);

        var fields = (descriptor?.CredentialFields ?? Array.Empty<ConnectorCredentialField>())
            .Select(field =>
            {
                var set = states.TryGetValue(field.Name, out var state) && state.IsSet;
                var version = states.TryGetValue(field.Name, out var v) ? v.Version : 0;
                return new CredentialFieldStateDto(field.Name, field.Label, field.Required, set, version);
            })
            .ToList();

        return new CredentialBundleListItem(
            bundle.Ref, bundle.Name, bundle.ConnectorKind, fields, boundConnectorIds, bundle.UpdatedAt ?? bundle.CreatedAt);
    }

    private static ConnectorKindSchemaDto MapKind(ConnectorKindDescriptor descriptor) =>
        new(
            descriptor.Kind,
            descriptor.ProviderCode,
            descriptor.Port.ToString(),
            descriptor.DisplayName,
            descriptor.CredentialFields.Select(f => new ConnectorCredentialFieldDto(f.Name, f.Label, f.Required)).ToList(),
            descriptor.ConfigFields
                .Select(f => new ConnectorConfigFieldDto(f.Name, f.Label, f.Required, f.AllowedValues, f.DefaultValue))
                .ToList(),
            descriptor.Environments.Select(e => e.Name).ToList());
}
