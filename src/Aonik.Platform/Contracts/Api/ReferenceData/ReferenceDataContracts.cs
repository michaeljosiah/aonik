namespace Aonik.Platform.Contracts.Api.ReferenceData;

public record ReferenceDataItemResponse(
    string Code,
    string DisplayName,
    int SortOrder);

public record ReferenceDataItemUpsertRequest(
    string DisplayName,
    int SortOrder,
    bool IsActive);

public record ReferenceDataItemAdminResponse(
    string Type,
    string Code,
    string DisplayName,
    int SortOrder,
    bool IsActive);
