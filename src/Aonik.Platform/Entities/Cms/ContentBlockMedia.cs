using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Cms;

public class ContentBlockMedia : Entity
{
    public Guid ContentBlockId { get; set; }
    
    public string StorageType { get; set; } = "Url";
    
    public string Url { get; set; } = string.Empty;
    
    public string? Alt { get; set; }
    
    public string? Caption { get; set; }
    
    public string? MimeType { get; set; }
    
    public int Order { get; set; } = 0;
    
    public string? LinkUrl { get; set; }
    
    public string? BlobContainer { get; set; }
    
    public string? BlobPath { get; set; }
    
    public long? FileSizeBytes { get; set; }
}
