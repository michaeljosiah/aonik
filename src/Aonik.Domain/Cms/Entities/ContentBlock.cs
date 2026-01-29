using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Cms.Entities;

public class ContentBlock : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    
    public string ContentKey { get; set; } = string.Empty;
    
    public string Title { get; set; } = string.Empty;
    
    public string? Slug { get; set; }
    
    public ContentBlockArea Area { get; set; } = ContentBlockArea.General;
    
    public ContentBlockFormat Format { get; set; } = ContentBlockFormat.Markdown;
    
    public string? Body { get; set; }
    
    public List<ContentBlockMedia> Media { get; set; } = new();
    
    public string Locale { get; set; } = "en";
    
    public bool IsEnabled { get; set; } = true;
    
    public DateTimeOffset? StartAt { get; set; }
    
    public DateTimeOffset? EndAt { get; set; }
    
    public int Priority { get; set; } = 100;
    
    public string? TargetingJson { get; set; } = "{}";
    
    public Guid? AiRunId { get; set; }
}
