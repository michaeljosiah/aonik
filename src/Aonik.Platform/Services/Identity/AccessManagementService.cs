using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Contracts.Services.Storage;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity.AccessManagement;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;

namespace Aonik.Platform.Services.Identity;

/// <summary>
/// Thin orchestrator for the access-management surface (users +
/// roles + permissions). Each method enforces the relevant
/// permission, resolves the current tenant, then dispatches to a
/// focused helper under the <c>AccessManagement</c> sibling folder.
/// The DI ctor signature is kept stable so consumers and tests
/// don't need to change.
/// </summary>
internal class AccessManagementService : AdminServiceBase, IAccessManagementService
{
    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICorrelationContext _correlationContext;
    private readonly IProfilePhotoStore _profilePhotoStore;
    private readonly IPendingTenantUserProvisioner _pendingUserProvisioner;

    public AccessManagementService(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IPermissionService permissionService,
        IClock clock,
        IAuditLogWriter auditLogWriter,
        ICorrelationContext correlationContext,
        IProfilePhotoStore profilePhotoStore,
        IPendingTenantUserProvisioner pendingUserProvisioner)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _auditLogWriter = auditLogWriter;
        _correlationContext = correlationContext;
        _profilePhotoStore = profilePhotoStore;
        _pendingUserProvisioner = pendingUserProvisioner;
    }

    // ─── Helper construction ─────────────────────────────────────
    // Helpers are constructed inline per call. They are tiny (just
    // grab references to already-injected deps) and allow the
    // orchestrator to remain stateless beyond its captured DI deps.

    private AccessUserQueryHelper Users() =>
        new(_dbContext, PermissionService);

    private AccessUserInviteHelper Invites() =>
        new(_dbContext, _clock, CurrentUserProvider, _auditLogWriter, _correlationContext, _pendingUserProvisioner);

    private AccessUserRoleHelper UserRoles() =>
        new(_dbContext, _clock, CurrentUserProvider, _auditLogWriter, _correlationContext);

    private AccessUserProfileHelper Profiles() =>
        new(_dbContext, _clock, CurrentUserProvider, _auditLogWriter, _correlationContext, _profilePhotoStore);

    private AccessUserLifecycleHelper Lifecycle() =>
        new(_dbContext, _clock, CurrentUserProvider, _auditLogWriter, _correlationContext);

    private AccessRoleHelper Roles() =>
        new(_dbContext, _clock, CurrentUserProvider);

    // ─── Users ───────────────────────────────────────────────────

    public async Task<PagedResult<AccessUserSummary>> ListUsersAsync(
        ListUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        return await Users().ListUsersAsync(tenantId, request, cancellationToken);
    }

    public async Task<AccessUserDetail?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        return await Users().GetUserAsync(tenantId, userId, cancellationToken);
    }

    public async Task<InviteUserResponse> InviteUserAsync(
        InviteUserRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Invite", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        return await Invites().InviteUserAsync(tenantId, request, cancellationToken);
    }

    public async Task UpdateUserRolesAsync(
        Guid userId,
        UpdateUserRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Manage", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await UserRoles().UpdateUserRolesAsync(tenantId, userId, request, cancellationToken);
    }

    public async Task UpdateUserProfileAsync(
        Guid userId,
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Manage", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await Profiles().UpdateUserProfileAsync(tenantId, userId, request, cancellationToken);
    }

    public async Task<CustomerPhotoUploadResponse?> UploadUserPhotoAsync(
        Guid userId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Manage", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        return await Profiles().UploadUserPhotoAsync(tenantId, userId, fileStream, fileName, contentType, cancellationToken);
    }

    public async Task<CustomerPhotoDeleteResponse?> DeleteUserPhotoAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Manage", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        return await Profiles().DeleteUserPhotoAsync(tenantId, userId, cancellationToken);
    }

    public async Task ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Manage", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await Lifecycle().ActivateUserAsync(tenantId, userId, cancellationToken);
    }

    public async Task DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Deactivate", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await Lifecycle().DeactivateUserAsync(tenantId, userId, cancellationToken);
    }

    public async Task<UserDiagnosticResult> DiagnoseUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        return await Lifecycle().DiagnoseUserAsync(tenantId, userId, cancellationToken);
    }

    public async Task<UserRepairResult> RepairUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Manage", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        return await Lifecycle().RepairUserAsync(tenantId, userId, cancellationToken);
    }

    // ─── Roles + Permissions ─────────────────────────────────────

    public async Task<PagedResult<AccessRoleSummary>> ListRolesAsync(
        ListRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        return await Roles().ListRolesAsync(tenantId, request, cancellationToken);
    }

    public async Task<AccessRoleDetail?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        return await Roles().GetRoleAsync(tenantId, roleId, cancellationToken);
    }

    public async Task<AccessRoleDetail> CreateRoleAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Create", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        return await Roles().CreateRoleAsync(tenantId, request, cancellationToken);
    }

    public async Task<AccessRoleDetail> UpdateRoleAsync(
        Guid roleId,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Update", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        return await Roles().UpdateRoleAsync(tenantId, roleId, request, cancellationToken);
    }

    public async Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Delete", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await Roles().DeleteRoleAsync(tenantId, roleId, cancellationToken);
    }

    public async Task UpdateRolePermissionsAsync(
        Guid roleId,
        UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Update", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await Roles().UpdateRolePermissionsAsync(tenantId, roleId, request, cancellationToken);
    }

    public async Task<List<PermissionDefinition>> ListPermissionsAsync(CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Permissions.Read", cancellationToken);
        return await Roles().ListPermissionsAsync(cancellationToken);
    }
}
