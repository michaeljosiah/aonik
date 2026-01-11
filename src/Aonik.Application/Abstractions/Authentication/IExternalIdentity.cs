namespace Aonik.Application.Abstractions.Authentication;

public interface IExternalIdentity
{
    Guid TenantId { get; }
    string ExternalIssuer { get; }
    string ExternalSubject { get; }
    string? ExternalTenantId { get; }
    string? Email { get; }
}
