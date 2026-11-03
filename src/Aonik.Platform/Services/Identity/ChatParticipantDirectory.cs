using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Platform;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Identity;

internal sealed class ChatParticipantDirectory(PlatformDbContext dbContext) : IChatParticipantDirectory
{
    private static readonly string[] WriteRoles = ["PlatformAdmin", "TenantAdmin", "Operations"];
    private static readonly string[] StaffRoles = ["PlatformAdmin", "TenantAdmin", "Operations", "ReadOnly"];

    public async Task<IReadOnlyList<ChatWorkspacePerson>> SearchAsync(Guid tenantId, string? search, CancellationToken cancellationToken = default)
    {
        var query = Eligible(tenantId);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(user => (user.Email ?? "").Contains(search.Trim()));
        var users = await query.OrderBy(user => user.Email).Take(50).Select(user => new { user.Id, user.Email, CanWrite = user.UserRoles.Any(link => link.Role.TenantId == tenantId && WriteRoles.Contains(link.Role.Name)) }).ToListAsync(cancellationToken);
        return users.Select(user => new ChatWorkspacePerson(user.Id, user.Email ?? $"Workspace member {user.Id.ToString()[..8]}", user.CanWrite)).ToList();
    }

    public async Task<IReadOnlyList<ChatWorkspacePerson>> GetByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var users = await Eligible(tenantId).Where(user => userIds.Contains(user.Id)).Select(user => new { user.Id, user.Email, CanWrite = user.UserRoles.Any(link => link.Role.TenantId == tenantId && WriteRoles.Contains(link.Role.Name)) }).ToListAsync(cancellationToken);
        return users.Select(user => new ChatWorkspacePerson(user.Id, user.Email ?? $"Workspace member {user.Id.ToString()[..8]}", user.CanWrite)).ToList();
    }

    private IQueryable<Aonik.Platform.Entities.Identity.User> Eligible(Guid tenantId) => dbContext.Users.AsNoTracking()
        .Where(user => user.TenantId == tenantId && user.Status == "Active" && user.ExternalIssuer != "aonik-bootstrap"
            && user.UserRoles.Any(link => link.Role.TenantId == tenantId && StaffRoles.Contains(link.Role.Name)));
}
