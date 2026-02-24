using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities;

/// <summary>
/// Read-only projection of the User entity for cross-module queries.
/// The authoritative User entity lives in Aonik.Platform.
/// TEMPORARY: Will be replaced by service contracts when inter-module
/// communication is fully implemented.
/// </summary>
public class UserReadModel : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string? Email { get; set; }
    public string Status { get; set; } = "Active";
}
