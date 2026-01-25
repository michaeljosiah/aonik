namespace Aonik.Application.Models.Features;

public record TenantFeatureToggle(
    string FeatureName,
    bool IsEnabled,
    string? Reason = null);

public record TenantFeatureState(
    string FeatureName,
    bool IsEnabled,
    DateTime? UpdatedAt);

public record TenantFeatureList(
    Guid TenantId,
    IReadOnlyList<TenantFeatureState> Features);
