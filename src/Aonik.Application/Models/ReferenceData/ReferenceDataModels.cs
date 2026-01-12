namespace Aonik.Application.Models.ReferenceData;

public record ReferenceDataItemSnapshot(
    string Type,
    string Code,
    string DisplayName,
    int SortOrder,
    bool IsActive);
