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

        // Note: FluentStorage Azure Blob operates at account level
        // Container name is used in the blob path operations
        return StorageFactory.Blobs.AzureBlobStorageWithSharedKey(
            azureOptions.AccountName,
            azureOptions.AccountKey);
    }
}
