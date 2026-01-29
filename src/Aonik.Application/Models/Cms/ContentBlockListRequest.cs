namespace Aonik.Application.Models.Cms;

public record ContentBlockListRequest(
    string? Area,
    string? ContentKey,
    string? Locale,
    bool? IsEnabled);
