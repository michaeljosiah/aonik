namespace Aonik.Api.Contracts.Cms;

public record AddContentBlockMediaRequest(
    string Url,
    string? Alt,
    string? Caption,
    string? MimeType,
    string? LinkUrl);
