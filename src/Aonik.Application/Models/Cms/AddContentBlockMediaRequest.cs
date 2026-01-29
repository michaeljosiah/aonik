namespace Aonik.Application.Models.Cms;

public record AddContentBlockMediaRequest(
    string Url,
    string? Alt,
    string? Caption,
    string? MimeType,
    string? LinkUrl);
