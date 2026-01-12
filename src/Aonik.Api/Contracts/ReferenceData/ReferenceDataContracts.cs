namespace Aonik.Api.Contracts.ReferenceData;

public record ReferenceDataItemResponse(
    string Code,
    string DisplayName,
    int SortOrder);
