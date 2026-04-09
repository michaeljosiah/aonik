using FluentStorage;
using FluentStorage.Blobs;
using Aonik.Application.Options;

namespace Aonik.Infrastructure.Storage;

/// <summary>
/// Factory for creating IBlobStorage instances based on configuration.
/// Supports multiple providers: Local, Azure, AWS, etc.
/// </summary>
public static class BlobStorageFactory
{
    /// <summary>
    /// Creates an IBlobStorage instance for a specific content type.
    /// </summary>
    /// <param name="options">Global blob storage configuration</param>
    /// <param name="contentTypeOptions">Content type specific configuration</param>
    /// <returns>Configured IBlobStorage instance</returns>
    public static IBlobStorage Create(BlobStorageOptions options, ContentTypeOptions contentTypeOptions)
    {
        return options.Provider.ToLowerInvariant() switch
        {
            "azure" => CreateAzureBlobStorage(options.Azure, contentTypeOptions),
            _ => CreateLocalStorage(options.LocalBasePath, contentTypeOptions.Path)
        };
    }

    private static IBlobStorage CreateLocalStorage(string basePath, string contentTypePath)
    {
        var fullPath = Path.Combine(basePath, contentTypePath);
        return StorageFactory.Blobs.DirectoryFiles(fullPath);
    }

    private static IBlobStorage CreateAzureBlobStorage(AzureBlobOptions azureOptions, ContentTypeOptions contentTypeOptions)
    {
        if (string.IsNullOrWhiteSpace(azureOptions.AccountName))
        {
            throw new InvalidOperationException("Azure Blob Storage AccountName is required when Provider is set to 'Azure'.");
        }

        if (string.IsNullOrWhiteSpace(azureOptions.AccountKey))
        {
            throw new InvalidOperationException("Azure Blob Storage AccountKey is required when Provider is set to 'Azure'.");
        }

        // FluentStorage Azure Blob operates at account level. The first path
        // segment of each blob path is treated as the container name.
        // We use a PrefixedBlobStorage wrapper so that callers write to
        // "customers/..." and the actual blob lands in "<containerName>/customers/...".
        var storage = StorageFactory.Blobs.AzureBlobStorageWithSharedKey(
            azureOptions.AccountName,
            azureOptions.AccountKey);

        if (!string.IsNullOrWhiteSpace(contentTypeOptions.ContainerName))
        {
            return new PrefixedBlobStorage(storage, contentTypeOptions.ContainerName);
        }

        return storage;
    }
}
