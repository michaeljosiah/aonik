namespace Aonik.Subscriptions.Contracts.Models;

// Spec 087 §6 — the catalogue's admin-facing shapes. Requests and responses only; entities never
// cross the service boundary.

public record CreateMeterRequest(string Code, string DisplayName, string Kind, string? Unit = null);

public record UpdateMeterRequest(string DisplayName, string? Unit);

public record MeterResponse(Guid Id, string Code, string DisplayName, string Kind, string? Unit);

public record CreatePlanRequest(
    string Code,
    string Name,
    string BillingInterval,
    string? Description = null,
    int SortOrder = 0);

public record UpdatePlanRequest(string Name, string? Description, int SortOrder);

public record PlanResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string BillingInterval,
    string Status,
    int SortOrder,
    IReadOnlyList<PlanVersionResponse> Versions);

public record CreatePlanVersionRequest(decimal Price, string Currency, DateTime? EffectiveFrom = null);

/// <summary>One entitlement on a draft version. Kind and unit are not supplied — they belong to the meter.</summary>
public record PlanEntitlementSpec(string MeterCode, decimal Allowance, string ResetPolicy);

public record SetEntitlementsRequest(IReadOnlyList<PlanEntitlementSpec> Entitlements);

public record PlanEntitlementResponse(
    Guid Id,
    string MeterCode,
    string MeterKind,
    string? MeterUnit,
    decimal Allowance,
    string ResetPolicy);

public record PlanVersionResponse(
    Guid Id,
    Guid PlanId,
    int Version,
    decimal Price,
    string Currency,
    DateTime EffectiveFrom,
    string Status,
    DateTime? PublishedAt,
    IReadOnlyList<PlanEntitlementResponse> Entitlements);
