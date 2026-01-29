namespace Aonik.Api.Contracts.Cms;

public record ContentBlockResponse(
    Guid Id,
    string ContentKey,
    string Title,
    string? Slug,
    string Area,
    string Format,
    string? Body,
    string Locale,
    bool IsEnabled,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    int Priority,
    List<ContentBlockMediaResponse> Media,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
