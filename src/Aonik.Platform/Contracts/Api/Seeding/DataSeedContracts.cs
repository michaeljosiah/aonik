namespace Aonik.Platform.Contracts.Api.Seeding;

public record DataSeedRequest(
    IReadOnlyList<string>? Keys);

public record DataSeedAvailableResponse(
    IReadOnlyList<DataSeedInfo> Seeds);

public record DataSeedInfo(
    string Key,
    string DisplayName,
    string Description,
    int SortOrder);

public record DataSeedResponse(
    DateTime SeededAt,
    IReadOnlyList<DataSeedResultItem> Results);

public record DataSeedResultItem(
    string Key,
    string DisplayName,
    IReadOnlyList<string> Operations);
