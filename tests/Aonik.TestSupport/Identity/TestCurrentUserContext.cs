using Aonik.SharedKernel.Abstractions;

namespace Aonik.TestSupport.Identity;

public sealed class TestCurrentUserContext : ICurrentUserContext
{
    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string? ExternalIssuer { get; set; }
    public string? ExternalSubject { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = [];
    public bool IsAuthenticated { get; set; }
}
