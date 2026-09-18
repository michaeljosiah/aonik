using System.Text;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Storage;

using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services.Safety;

/// <summary>
/// Preserves a child's input that a classifier judged reportable (Spec 096 §12), in the
/// platform's file store under the subject's own key, and hands back the storage key the incident
/// artefact records. Retention and legal hold are the artefact's business
/// (<see cref="PreservedMaterialService"/>); this class only makes sure the bytes exist to hold.
/// </summary>
internal sealed class FilePreservedInputStore : IPreservedInputStore
{
    private readonly IFileStore _files;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<FilePreservedInputStore> _logger;

    public FilePreservedInputStore(IFileStore files, ITenantProvider tenantProvider, ILogger<FilePreservedInputStore> logger)
    {
        _files = files;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<string> PreserveAsync(Guid subjectPartyId, string input, CancellationToken cancellationToken = default)
    {
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(input));

        // The owner is the subject: what is preserved is theirs, and access to it is decided per
        // incident by the material service, never by whoever can read the store.
        var stored = await _files.UploadAsync(
            _tenantProvider.GetCurrentTenantId(), subjectPartyId, content, $"preserved-input-{Guid.NewGuid():N}.txt", "text/plain; charset=utf-8", cancellationToken);

        _logger.LogWarning(
            "Preserved reportable input for subject {SubjectId} as {StorageKey} ({Bytes} bytes).",
            subjectPartyId, stored.StorageKey, stored.FileSizeBytes);

        return stored.StorageKey;
    }
}
