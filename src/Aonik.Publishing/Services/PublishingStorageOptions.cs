namespace Aonik.Publishing.Services;

public sealed class PublishingStorageOptions
{
    // Local files retain owner-only roots. Azure Files relies on the dedicated,
    // authenticated SMB mount, which is verified before any object is written.
    public string AccessControl { get; set; } = "Local";
    public string? AzureFilesAccountName { get; set; }
}
