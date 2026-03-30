namespace Aonik.Platform.Contracts.Models.Cms;

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
    string? TargetingJson,
    Guid? AiRunId,
    List<ContentBlockMediaResponse> Media,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ContentBlockMediaResponse(
    Guid Id,
    string StorageType,
    string Url,
    string? Alt,
    string? Caption,
    string? MimeType,
    int Order,
    string? LinkUrl);

public record CreateContentBlockRequest(
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
    string? TargetingJson,
    Guid? AiRunId = null);

public record UpdateContentBlockRequest(
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
    string? TargetingJson);

public record ContentBlockListRequest(
    string? Area,
    string? ContentKey,
    string? Locale,
    bool? IsEnabled);

public record AddContentBlockMediaRequest(
    string Url,
    string? Alt,
    string? Caption,
    string? MimeType,
    string? LinkUrl);

public record ReorderContentBlockMediaRequest(
    List<Guid> MediaIds);
