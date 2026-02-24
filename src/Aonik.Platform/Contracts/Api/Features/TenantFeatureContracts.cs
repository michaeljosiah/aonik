namespace Aonik.Platform.Contracts.Api.Features;

public record TenantFeatureToggleRequest(
    string FeatureName,
    bool IsEnabled,
    string? Reason = null);

public record TenantFeatureUpdateRequest(
    IReadOnlyList<TenantFeatureToggleRequest> Features);

public record TenantFeatureItemResponse(
    string FeatureName,
    bool IsEnabled,
    DateTime? UpdatedAt);

public record TenantFeatureListResponse(
    Guid TenantId,
    IReadOnlyList<TenantFeatureItemResponse> Features);
