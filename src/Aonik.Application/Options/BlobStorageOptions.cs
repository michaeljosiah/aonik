namespace Aonik.Application.Options;

/// <summary>
/// Configuration for blob storage providers and content types.
/// </summary>
public class BlobStorageOptions
{
    /// <summary>
    /// Storage provider: Local, Azure, AWS, etc.
    /// </summary>
    public string Provider { get; set; } = "Local";

    /// <summary>
    /// Base path for local file system storage.
    /// </summary>
    public string LocalBasePath { get; set; } = "App_Data";

    /// <summary>
    /// Azure Blob Storage configuration.
    /// </summary>
    public AzureBlobOptions Azure { get; set; } = new();

    /// <summary>
    /// Configuration for profile photos.
    /// </summary>
    public ContentTypeOptions ProfilePhotos { get; set; } = new()
    {
        Path = "profiles",
        ContainerName = "profiles",
        PublicBaseUrl = null
    };

    /// <summary>
    /// Configuration for product images.
    /// </summary>
    public ContentTypeOptions ProductImages { get; set; } = new()
    {
        Path = "products",
        ContainerName = "products",
        PublicBaseUrl = null
    };

    /// <summary>
    /// Configuration for documents.
    /// </summary>
    public ContentTypeOptions Documents { get; set; } = new()
    {
        Path = "documents",
        ContainerName = "documents",
        PublicBaseUrl = null
    };

    /// <summary>
    /// Configuration for attachments (transaction receipts, etc.).
    /// </summary>
    public ContentTypeOptions Attachments { get; set; } = new()
    {
        Path = "attachments",
        ContainerName = "attachments",
        PublicBaseUrl = null
    };

    /// <summary>
    /// Configuration for AI-generated content media (hero images, etc.).
    /// </summary>
    public ContentTypeOptions ContentMedia { get; set; } = new()
    {
        Path = "content-media",
        ContainerName = "content-media",
        PublicBaseUrl = null
    };
}

/// <summary>
/// Azure Blob Storage specific configuration.
/// </summary>
public class AzureBlobOptions
{
    public string? AccountName { get; set; }
    public string? AccountKey { get; set; }
}

/// <summary>
/// Configuration for a specific content type (e.g., profile photos, product images).
/// </summary>
public class ContentTypeOptions
{
    /// <summary>
    /// Path/prefix for this content type (used in local storage and as blob prefix).
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Azure container name for this content type.
    /// </summary>
    public string? ContainerName { get; set; }

    /// <summary>
    /// Public base URL for serving files (optional, for CDN or public access).
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
