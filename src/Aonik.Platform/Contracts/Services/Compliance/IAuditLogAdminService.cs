using Aonik.Platform.Contracts.Models.Compliance;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Contracts.Services.Compliance;

public interface IAuditLogAdminService
{
    Task<PagedResult<AuditLogListItem>> ListAuditLogsAsync(
        ListAuditLogsRequest request,
        CancellationToken cancellationToken = default);
}
