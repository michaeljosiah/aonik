namespace Aonik.Platform.Contracts.Services.Authentication;

public interface IExternalIdentity
{
    Guid TenantId { get; }
    string ExternalIssuer { get; }
    string ExternalSubject { get; }
    string? ExternalTenantId { get; }
    string? Email { get; }
}
