using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Aonik.Application.Abstractions.Storage;
using Aonik.Application.Options;
using Aonik.Platform.Contracts.Models.ContactEnquiries;
using Aonik.SharedKernel.Abstractions.Storage;

namespace Aonik.Infrastructure.Storage;

/// <summary>Uses the shared private file store and preserves the contact-enquiry error contract.</summary>
public sealed class ContactImageFileStore(IBlobStorageFactory factory, IOptions<BlobStorageOptions> options,
    IHostEnvironment environment) : IFileStore
{
    private readonly PrivateFileStore _files = new(factory, options, options.Value.ContactImages, environment);
    public async Task<FileUploadResult> UploadAsync(Guid tenantId, Guid ownerEntityId, Stream fileStream, string fileName,
        string contentType, CancellationToken cancellationToken = default)
    {
        try { return await _files.UploadAsync(tenantId, ownerEntityId, fileStream, fileName, contentType, cancellationToken); }
        catch (InvalidOperationException) { throw Unavailable(); }
    }
    public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        try { return await _files.OpenReadAsync(storageKey, cancellationToken); }
        catch (InvalidOperationException) { throw Unavailable(); }
    }
    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        try { await _files.DeleteAsync(storageKey, cancellationToken); }
        catch (InvalidOperationException) { throw Unavailable(); }
    }
    public string GetUrl(string storageKey) => throw new NotSupportedException("Contact photos are only available through authorized enquiry endpoints.");
    public Task<StagedBlob> StageAsync(Guid tenantId, Stream content, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Contact photos use direct private uploads.");
    public Task<PromoteResult> PromoteAsync(StagedBlob staged, string contentKey, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Contact photos use direct private uploads.");
    private static ContactEnquiryUnavailableException Unavailable() => new("Private photo storage is unavailable. Please try again later.");
}
