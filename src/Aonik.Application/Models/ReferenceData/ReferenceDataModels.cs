namespace Aonik.Application.Models.ReferenceData;

public record ReferenceDataItemSnapshot(
    string Type,
    string Code,
    string DisplayName,
    int SortOrder,
    bool IsActive);

public record ReferenceDataItemUpsert(
    string Type,
    string Code,
    string DisplayName,
    int SortOrder,
    bool IsActive);
