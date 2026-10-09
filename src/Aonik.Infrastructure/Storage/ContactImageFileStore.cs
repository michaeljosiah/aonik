using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Aonik.Application.Abstractions.Storage;
using Aonik.Application.Options;
using Aonik.Platform.Contracts.Models.ContactEnquiries;
using Aonik.SharedKernel.Abstractions.Storage;

namespace Aonik.Infrastructure.Storage;

/// <summary>Uses the existing file store, but never exposes contact photos through public storage.</summary>
public sealed class ContactImageFileStore(
    IBlobStorageFactory factory,
    IOptions<BlobStorageOptions> options,
    IHostEnvironment environment) : IFileStore
{
    private FileStore? _store;

    public async Task<FileUploadResult> UploadAsync(Guid tenantId, Guid ownerEntityId, Stream fileStream,
        string fileName, string contentType, CancellationToken cancellationToken = default)
        => await (await GetPrivateStoreAsync(cancellationToken))
            .UploadAsync(tenantId, ownerEntityId, fileStream, fileName, contentType, cancellationToken);

    public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
        => await (await GetPrivateStoreAsync(cancellationToken)).OpenReadAsync(storageKey, cancellationToken);

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        => await (await GetPrivateStoreAsync(cancellationToken)).DeleteAsync(storageKey, cancellationToken);

    public string GetUrl(string storageKey)
        => throw new NotSupportedException("Contact photos are only available through authorized enquiry endpoints.");

    public Task<StagedBlob> StageAsync(Guid tenantId, Stream content, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Contact photos use direct private uploads.");

    public Task<PromoteResult> PromoteAsync(StagedBlob staged, string contentKey, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Contact photos use direct private uploads.");

    private async Task<FileStore> GetPrivateStoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            BlobStorageOptions settings = options.Value;
            ContentTypeOptions contact = settings.ContactImages;
            ContentTypeOptions[] publicStores = [settings.ProfilePhotos, settings.ProductImages,
                settings.Documents, settings.Attachments, settings.ContentMedia];
            if (!string.IsNullOrWhiteSpace(contact.PublicBaseUrl))
                throw Unavailable();

            if (string.Equals(settings.Provider, "Local", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(contact.Path) || Path.IsPathRooted(contact.Path))
                    throw Unavailable();
                string contactPath = Path.GetFullPath(Path.Combine(settings.LocalBasePath, contact.Path));
                string? configuredWebRoot = (environment as IWebHostEnvironment)?.WebRootPath;
                string webRoot = string.IsNullOrWhiteSpace(configuredWebRoot)
                    ? Path.Combine(environment.ContentRootPath, "wwwroot") : configuredWebRoot;
                string[] publicPaths = publicStores.Select(store =>
                    Path.GetFullPath(Path.Combine(settings.LocalBasePath, store.Path)))
                    .Append(Path.GetFullPath(webRoot)).ToArray();
                if (publicPaths.Any(path => Overlaps(contactPath, path)))
                    throw Unavailable();

                // Either side can alias the other: a public root junction can expose an ordinary
                // private directory just as a private junction can write into a public directory.
                foreach (var path in publicPaths.Append(contactPath))
                    for (DirectoryInfo? directory = new(path); directory != null; directory = directory.Parent)
                        if (directory.Exists && directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                            throw Unavailable();
            }
            else if (string.Equals(settings.Provider, "Azure", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(contact.ContainerName) ||
                    string.IsNullOrWhiteSpace(settings.Azure.AccountName) ||
                    string.IsNullOrWhiteSpace(settings.Azure.AccountKey) || publicStores.Any(store =>
                        string.Equals(contact.ContainerName, store.ContainerName, StringComparison.OrdinalIgnoreCase)))
                    throw Unavailable();
                var clientOptions = new BlobClientOptions();
                clientOptions.Retry.MaxRetries = 0;
                clientOptions.Retry.NetworkTimeout = TimeSpan.FromSeconds(10);
                var container = new BlobContainerClient(
                    new Uri($"https://{settings.Azure.AccountName}.blob.core.windows.net/{contact.ContainerName}"),
                    new StorageSharedKeyCredential(settings.Azure.AccountName, settings.Azure.AccountKey), clientOptions);
                await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
                var policy = await container.GetAccessPolicyAsync(cancellationToken: cancellationToken);
                if (policy.Value.BlobPublicAccess != PublicAccessType.None)
                    throw Unavailable();
            }
            else
            {
                throw Unavailable();
            }

            return _store ??= new FileStore(factory, options, contact);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ContactEnquiryUnavailableException) { throw; }
        catch (Exception) { throw Unavailable(); }
    }

    private static bool Overlaps(string left, string right)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        left = Path.TrimEndingDirectorySeparator(left);
        right = Path.TrimEndingDirectorySeparator(right);
        return left.Equals(right, comparison) ||
            left.StartsWith(right + Path.DirectorySeparatorChar, comparison) ||
            right.StartsWith(left + Path.DirectorySeparatorChar, comparison);
    }

    private static ContactEnquiryUnavailableException Unavailable()
        => new("Private photo storage is unavailable. Please try again later.");
}
