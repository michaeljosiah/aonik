using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Partners.Connectors.Registry;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Partners.Connectors.Credentials;

/// <summary>A bundle resolved and decrypted for server-side use at connector-build time (Spec 042 §7).</summary>
internal sealed class ResolvedCredentialBundle
{
    public required string Ref { get; init; }
    public required string ConnectorKind { get; init; }
    public required CredentialSecretStore Secrets { get; init; }
}

/// <summary>Fields written to a bundle. Only keys present are changed; omitted keys keep their stored value.</summary>
internal sealed record CredentialBundleWriteRequest(
    string Ref,
    string Name,
    string ConnectorKind,
    IReadOnlyDictionary<string, string> Secrets);

/// <summary>
/// Reads, writes, and rotates <see cref="CredentialBundle"/>s (Spec 042 §6, §11). Owns the only path that
/// decrypts bundle secrets; callers receive a <see cref="ResolvedCredentialBundle"/> for build-time use and
/// never the ciphertext. All operations are tenant-scoped through <see cref="FinanceDbContext"/>'s query
/// filter, so a cross-tenant <c>Ref</c> simply resolves to nothing.
/// </summary>
internal interface ICredentialBundleService
{
    /// <summary>Resolves and decrypts a bundle by its immutable <c>Ref</c>, or null if none in this tenant.</summary>
    Task<ResolvedCredentialBundle?> ResolveAsync(string credentialsRef, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a bundle, validating fields against the connector kind's credential schema.</summary>
    Task<CredentialBundle> UpsertAsync(CredentialBundleWriteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates a verifier field (e.g. the webhook signing secret) keeping the previous value valid for
    /// <paramref name="previousTtl"/> (default 24h) — Spec 042 §11. Returns false if the bundle is not found.
    /// </summary>
    Task<bool> RotateFieldAsync(
        string credentialsRef, string field, string newValue, TimeSpan? previousTtl = null,
        CancellationToken cancellationToken = default);

    /// <summary>The value-free field state (set/not-set + version) for "Configured" badges — never values.</summary>
    Task<IReadOnlyList<CredentialFieldState>> GetFieldStatesAsync(
        string credentialsRef, CancellationToken cancellationToken = default);
}

internal sealed class CredentialBundleService : ICredentialBundleService
{
    private static readonly TimeSpan DefaultRotationGrace = TimeSpan.FromHours(24);

    private readonly FinanceDbContext _dbContext;
    private readonly IConnectorCredentialProtector _protector;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public CredentialBundleService(
        FinanceDbContext dbContext,
        IConnectorCredentialProtector protector,
        ITenantProvider tenantProvider,
        IClock clock)
    {
        _dbContext = dbContext;
        _protector = protector;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<ResolvedCredentialBundle?> ResolveAsync(
        string credentialsRef, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentialsRef))
        {
            return null;
        }

        var normalized = credentialsRef.Trim();
        var bundle = await _dbContext.CredentialBundles
            .FirstOrDefaultAsync(b => b.Ref == normalized, cancellationToken);

        if (bundle is null)
        {
            return null;
        }

        return new ResolvedCredentialBundle
        {
            Ref = bundle.Ref,
            ConnectorKind = bundle.ConnectorKind,
            Secrets = DecryptSecrets(bundle.ProtectedSecretsJson),
        };
    }

    public async Task<CredentialBundle> UpsertAsync(
        CredentialBundleWriteRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Ref))
        {
            throw new InvalidOperationException("A credential bundle Ref is required.");
        }

        var descriptor = ConnectorRegistry.GetRequired(request.ConnectorKind);
        var normalizedRef = request.Ref.Trim();

        // Reject any secret key the kind does not declare (Spec 042 §10 — no unknown fields).
        foreach (var key in request.Secrets.Keys)
        {
            if (descriptor.Credential(key) is null)
            {
                throw new InvalidOperationException(
                    $"Credential field '{key}' is not valid for connector kind '{descriptor.Kind}'.");
            }
        }

        var bundle = await _dbContext.CredentialBundles
            .FirstOrDefaultAsync(b => b.Ref == normalizedRef, cancellationToken);
        var isNew = bundle is null;

        if (isNew)
        {
            // On create, every required credential field must be supplied with a non-empty value.
            foreach (var field in descriptor.CredentialFields.Where(f => f.Required))
            {
                if (!request.Secrets.TryGetValue(field.Name, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException(
                        $"Credential field '{field.Name}' is required for connector kind '{descriptor.Kind}'.");
                }
            }

            bundle = new CredentialBundle
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantProvider.GetCurrentTenantId(),
                Ref = normalizedRef,
                ConnectorKind = descriptor.Kind,
            };
            _dbContext.CredentialBundles.Add(bundle);
        }

        var store = isNew ? new CredentialSecretStore() : DecryptSecrets(bundle!.ProtectedSecretsJson);
        var versions = CurrentVersions(bundle!.FieldMetadataJson);

        foreach (var (key, value) in request.Secrets)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            store.Set(key, value);
            versions[key] = versions.TryGetValue(key, out var existing) ? existing + 1 : 1;
        }

        bundle!.Name = string.IsNullOrWhiteSpace(request.Name) ? normalizedRef : request.Name.Trim();
        bundle.ConnectorKind = descriptor.Kind;
        bundle.ProtectedSecretsJson = _protector.Protect(store.Serialize());
        bundle.FieldMetadataJson = BuildMetadata(descriptor, store, versions);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return bundle;
    }

    public async Task<bool> RotateFieldAsync(
        string credentialsRef, string field, string newValue, TimeSpan? previousTtl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            throw new InvalidOperationException("A new secret value is required to rotate a credential field.");
        }

        var normalizedRef = credentialsRef.Trim();
        var bundle = await _dbContext.CredentialBundles
            .FirstOrDefaultAsync(b => b.Ref == normalizedRef, cancellationToken);
        if (bundle is null)
        {
            return false;
        }

        var descriptor = ConnectorRegistry.GetRequired(bundle.ConnectorKind);
        if (descriptor.Credential(field) is null)
        {
            throw new InvalidOperationException(
                $"Credential field '{field}' is not valid for connector kind '{descriptor.Kind}'.");
        }

        var store = DecryptSecrets(bundle.ProtectedSecretsJson);
        store.Rotate(field, newValue, _clock.UtcNow.Add(previousTtl ?? DefaultRotationGrace));

        var versions = CurrentVersions(bundle.FieldMetadataJson);
        versions[field] = versions.TryGetValue(field, out var existing) ? existing + 1 : 1;

        bundle.ProtectedSecretsJson = _protector.Protect(store.Serialize());
        bundle.FieldMetadataJson = BuildMetadata(descriptor, store, versions);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<CredentialFieldState>> GetFieldStatesAsync(
        string credentialsRef, CancellationToken cancellationToken = default)
    {
        var normalizedRef = credentialsRef.Trim();
        var bundle = await _dbContext.CredentialBundles
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Ref == normalizedRef, cancellationToken);

        return bundle is null
            ? Array.Empty<CredentialFieldState>()
            : CredentialFieldMetadata.Deserialize(bundle.FieldMetadataJson);
    }

    private CredentialSecretStore DecryptSecrets(string protectedSecretsJson)
    {
        if (string.IsNullOrWhiteSpace(protectedSecretsJson))
        {
            return new CredentialSecretStore();
        }

        var plaintext = _protector.Unprotect(protectedSecretsJson);
        return CredentialSecretStore.Deserialize(plaintext);
    }

    private static Dictionary<string, int> CurrentVersions(string fieldMetadataJson) =>
        CredentialFieldMetadata.Deserialize(fieldMetadataJson)
            .ToDictionary(s => s.Name, s => s.Version, StringComparer.OrdinalIgnoreCase);

    private static string BuildMetadata(
        ConnectorKindDescriptor descriptor, CredentialSecretStore store, IReadOnlyDictionary<string, int> versions)
    {
        var states = descriptor.CredentialFields.Select(field =>
        {
            var isSet = store.Has(field.Name);
            var version = versions.TryGetValue(field.Name, out var v) ? v : 0;
            if (isSet && version == 0)
            {
                version = 1;
            }

            return new CredentialFieldState(field.Name, isSet, version);
        });

        return CredentialFieldMetadata.Serialize(states);
    }
}
