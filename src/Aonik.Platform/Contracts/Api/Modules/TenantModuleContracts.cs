namespace Aonik.Platform.Contracts.Api.Modules;

/// <summary>Wire shape of one module in <see cref="TenantModuleListResponse"/> (Spec 097 §9).</summary>
/// <param name="Source">"core", "default", "pack" or "explicit".</param>
public record TenantModuleItemResponse(
    string ModuleId,
    string Name,
    string Description,
    bool IsCore,
    IReadOnlyList<string> DependsOn,
    IReadOnlyList<string> SoftDependsOn,
    bool IsEnabled,
    string Source,
    string? Reason,
    DateTime? UpdatedAt,
    Guid? UpdatedBy);

/// <summary>Response of GET and PUT <c>/admin/tenants/{tenantId}/modules</c>: every catalogue module with state.</summary>
public record TenantModuleListResponse(
    Guid TenantId,
    IReadOnlyList<TenantModuleItemResponse> Modules);

/// <summary>One toggle in <see cref="TenantModuleUpdateRequest"/>.</summary>
public record TenantModuleToggleRequest(
    string ModuleId,
    bool IsEnabled,
    string? Reason = null);

/// <summary>Body of PUT <c>/admin/tenants/{tenantId}/modules</c>: known, non-core, non-duplicated ids only.</summary>
public record TenantModuleUpdateRequest(
    IReadOnlyList<TenantModuleToggleRequest> Modules);
