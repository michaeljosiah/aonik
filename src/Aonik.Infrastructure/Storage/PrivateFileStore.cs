using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Aonik.Application.Abstractions.Storage;
using Aonik.Application.Options;
using Aonik.SharedKernel.Abstractions.Storage;

namespace Aonik.Infrastructure.Storage;

/// <summary>The existing file store behind checked private storage roots/containers and authorized application endpoints.</summary>
internal sealed class PrivateFileStore(IBlobStorageFactory factory, IOptions<BlobStorageOptions> options,
    ContentTypeOptions content, IHostEnvironment environment) : IFileStore
{
    private FileStore? _store;
    private string? _privatePath;
    private BlobContainerClient? _privateContainer;
    public async Task<FileUploadResult> UploadAsync(Guid tenantId, Guid ownerEntityId, Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
        => await (await GetStoreAsync(cancellationToken)).UploadAsync(tenantId, ownerEntityId, fileStream, fileName, contentType, cancellationToken);
    public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
        => await (await GetStoreAsync(cancellationToken)).OpenReadAsync(storageKey, cancellationToken);
    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        => await (await GetStoreAsync(cancellationToken)).DeleteAsync(storageKey, cancellationToken);
    public string GetUrl(string storageKey) => throw new NotSupportedException("Private content requires an authorized application endpoint.");
    public async Task<StagedBlob> StageAsync(Guid tenantId, Stream stream, CancellationToken cancellationToken = default)
        => await (await GetStoreAsync(cancellationToken)).StageAsync(tenantId, stream, cancellationToken);
    public async Task<PromoteResult> PromoteAsync(StagedBlob staged, string key, CancellationToken cancellationToken = default)
        => await (await GetStoreAsync(cancellationToken)).PromoteAsync(staged, key, cancellationToken);

    internal async Task<int> RemoveExpiredConnectorUploadsAsync(DateTime cutoffUtc, int limit, CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(content, options.Value.ConnectorContent)) throw Unavailable();
        var removed = 0;
        await GetStoreAsync(cancellationToken);
        if (_privateContainer is { } container)
        {
            await foreach (BlobItem blob in container.GetBlobsAsync(prefix: "tenants/", cancellationToken: cancellationToken))
            {
                if (!IsConnectorUpload(blob.Name) || blob.Properties.LastModified is not { } modified || modified.UtcDateTime > cutoffUtc
                    || blob.Properties.ETag is not { } etag) continue;
                try
                {
                    if ((await container.DeleteBlobIfExistsAsync(blob.Name, DeleteSnapshotsOption.IncludeSnapshots,
                        new BlobRequestConditions { IfMatch = etag }, cancellationToken)).Value && ++removed == limit) break;
                }
                catch (Azure.RequestFailedException exception) when (exception.Status == 412) { }
            }
            return removed;
        }

        string root = _privatePath ?? throw Unavailable();
        string uploads = Path.Combine(root, "tenants");
        if (!Directory.Exists(uploads) || File.GetAttributes(uploads).HasFlag(FileAttributes.ReparsePoint)) return 0;
        var enumeration = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 2,
            AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };
        foreach (string file in Directory.EnumerateFiles(uploads, "*", enumeration))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            if (!IsConnectorUpload(name) || File.GetLastWriteTimeUtc(file) > cutoffUtc) continue;
            File.Delete(file);
            if (++removed == limit) break;
        }
        return removed;
    }

    private static bool IsConnectorUpload(string name)
    {
        string[] segments = name.Split('/');
        return segments.Length == 4 && segments[0] == "tenants"
            && Guid.TryParseExact(segments[1], "N", out _) && Guid.TryParseExact(segments[2], "N", out _)
            && Guid.TryParseExact(Path.GetFileNameWithoutExtension(segments[3]), "N", out _);
    }

    private async Task<FileStore> GetStoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            BlobStorageOptions settings = options.Value;
            ContentTypeOptions[] otherStores = [settings.ProfilePhotos, settings.ProductImages, settings.Documents,
                settings.Attachments, settings.ContentMedia, settings.ContactImages, settings.ConnectorContent, settings.PrivateDocuments];
            otherStores = otherStores.Where(other => !ReferenceEquals(other, content)).ToArray();
            if (!string.IsNullOrWhiteSpace(content.PublicBaseUrl)) throw Unavailable();
            if (string.Equals(settings.Provider, "Local", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(content.Path) || Path.IsPathRooted(content.Path)) throw Unavailable();
                string privatePath = Path.GetFullPath(Path.Combine(settings.LocalBasePath, content.Path));
                string storageRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(settings.LocalBasePath)) + Path.DirectorySeparatorChar;
                if (!privatePath.StartsWith(storageRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw Unavailable();
                string? configuredWebRoot = (environment as IWebHostEnvironment)?.WebRootPath;
                string webRoot = string.IsNullOrWhiteSpace(configuredWebRoot) ? Path.Combine(environment.ContentRootPath, "wwwroot") : configuredWebRoot;
                string[] exposedPaths = otherStores.Select(other => Path.GetFullPath(Path.Combine(settings.LocalBasePath, other.Path)))
                    .Append(Path.GetFullPath(webRoot)).ToArray();
                if (exposedPaths.Any(path => Overlaps(privatePath, path))) throw Unavailable();
                foreach (string path in exposedPaths.Append(privatePath))
                    for (DirectoryInfo? directory = new(path); directory is not null; directory = directory.Parent)
                        if (directory.Exists && directory.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw Unavailable();
                _privatePath = privatePath;
            }
            else if (string.Equals(settings.Provider, "Azure", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(content.ContainerName) || string.IsNullOrWhiteSpace(settings.Azure.AccountName)
                    || string.IsNullOrWhiteSpace(settings.Azure.AccountKey) || otherStores.Any(other => string.Equals(content.ContainerName, other.ContainerName, StringComparison.OrdinalIgnoreCase)))
                    throw Unavailable();
                var clientOptions = new BlobClientOptions();
                clientOptions.Retry.MaxRetries = 0;
                clientOptions.Retry.NetworkTimeout = TimeSpan.FromSeconds(10);
                var container = new BlobContainerClient(new Uri($"https://{settings.Azure.AccountName}.blob.core.windows.net/{content.ContainerName}"),
                    new StorageSharedKeyCredential(settings.Azure.AccountName, settings.Azure.AccountKey), clientOptions);
                await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
                if ((await container.GetAccessPolicyAsync(cancellationToken: cancellationToken)).Value.BlobPublicAccess != PublicAccessType.None) throw Unavailable();
                _privateContainer = container;
            }
            else throw Unavailable();
            return _store ??= new FileStore(factory, options, content);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { throw Unavailable(); }
    }
    private static bool Overlaps(string left, string right)
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        left = Path.TrimEndingDirectorySeparator(left); right = Path.TrimEndingDirectorySeparator(right);
        return left.Equals(right, comparison) || left.StartsWith(right + Path.DirectorySeparatorChar, comparison)
            || right.StartsWith(left + Path.DirectorySeparatorChar, comparison);
    }
    internal static InvalidOperationException Unavailable() => new("Private file storage is unavailable.");
}
