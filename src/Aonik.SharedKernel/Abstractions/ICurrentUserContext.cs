namespace Aonik.SharedKernel.Abstractions;

public interface ICurrentUserContext
{
    Guid? UserId { get; set; }
    Guid? TenantId { get; set; }
    string? ExternalIssuer { get; set; }
    string? ExternalSubject { get; set; }
    IReadOnlyCollection<string> Roles { get; set; }
    bool IsAuthenticated { get; set; }
}
