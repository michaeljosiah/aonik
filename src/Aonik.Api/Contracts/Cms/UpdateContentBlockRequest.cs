namespace Aonik.Api.Contracts.Cms;

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
