using System.Security.Cryptography;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Aonik.Application.Abstractions.Storage;
using Aonik.Application.Options;
using Aonik.Infrastructure.Storage;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Storage;

namespace Aonik.Infrastructure.Integrations;

internal sealed class ConnectorContentStore(IBlobStorageFactory factory, IOptions<BlobStorageOptions> options,
    IHostEnvironment environment, IDataProtectionProvider protection, ITenantProvider tenantProvider, IClock clock) : IConnectorContentStore, IConnectorContentRetention
{
    private readonly PrivateFileStore _files = new(factory, options, options.Value.ConnectorContent, environment);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromMinutes(2));
        // Allow the protected reference to expire even when host and storage timestamps differ slightly.
        DateTime cutoff = clock.UtcNow - Retention - TimeSpan.FromMinutes(10);
        return await _files.RemoveExpiredConnectorUploadsAsync(cutoff, 500, budget.Token);
    }

    public async Task<ConnectorStoredContent> SaveAsync(Guid connectionId, string messageId, string? attachmentId, byte[] content,
        string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        if (content.Length is 0 or > 10 * 1024 * 1024) throw new ConnectorProviderException("ContentSizeUnsupported");
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        using var stream = new MemoryStream(content, writable: false);
        FileUploadResult stored;
        try { stored = await _files.UploadAsync(tenantId, connectionId, stream, fileName, contentType, cancellationToken); }
        catch (InvalidOperationException) { throw new ConnectorProviderException("PrivateStorageUnavailable"); }
        DateTime expiresAt = clock.UtcNow + Retention;
        var metadata = new ConnectorStoredContent("", connectionId, messageId, attachmentId, stored.FileName, stored.ContentType,
            stored.FileSizeBytes, stored.Sha256, expiresAt);
        string reference = Protector(tenantId).Protect(ConnectorJson.Serialize(new Envelope(metadata, stored.StorageKey)));
        return metadata with { FileReference = reference };
    }
    public ConnectorStoredContent Describe(string fileReference) => EnvelopeFor(fileReference).Metadata with { FileReference = fileReference };
    public async Task<Stream> OpenAsync(string fileReference, CancellationToken cancellationToken = default)
    {
        Envelope envelope = EnvelopeFor(fileReference);
        return await _files.OpenReadAsync(envelope.StorageKey, cancellationToken) ?? throw new ConnectorAccessException("ContentExpired", "The protected connector content is unavailable.");
    }
    private Envelope EnvelopeFor(string reference)
    {
        try
        {
            if (reference.Length > 8192) throw new ArgumentException("Invalid content reference.");
            Envelope envelope = ConnectorJson.Read<Envelope>(Protector(tenantProvider.GetCurrentTenantId()).Unprotect(reference));
            if (envelope.Metadata.ExpiresAt <= clock.UtcNow) throw new ConnectorAccessException("ContentExpired", "The protected content reference expired.");
            return envelope;
        }
        catch (Exception exception) when (exception is CryptographicException or System.Text.Json.JsonException or ArgumentException)
        { throw new ConnectorAccessException("ContentUnavailable", "The protected content reference is unavailable."); }
    }
    private IDataProtector Protector(Guid tenantId) => protection.CreateProtector("Aonik.ConnectorContent.v1", tenantId.ToString("D"));
    private sealed record Envelope(ConnectorStoredContent Metadata, string StorageKey);
}
