namespace Aonik.Application.Models.Cms;

public record ContentBlockMediaResponse(
    Guid Id,
    string StorageType,
    string Url,
    string? Alt,
    string? Caption,
    string? MimeType,
    int Order,
    string? LinkUrl);
