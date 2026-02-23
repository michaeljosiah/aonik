namespace Aonik.Platform.Contracts.Models.Identity;

public record ListTenantsRequest(
    int PageNumber = 1,
    int PageSize = 20,
    string? Environment = null,
    string? Status = null,
    string? NameFilter = null
);
