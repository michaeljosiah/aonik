using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.SignupLists;

public class SignupSubscription : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string ListType { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string? Postcode { get; set; }
    public string? PostcodeOutwardCode { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? CountryCode { get; set; }
    public string? Service { get; set; }
    public string ConsentVersion { get; set; } = string.Empty;
    public string ConsentTextSnapshot { get; set; } = string.Empty;
    public string ConsentSource { get; set; } = string.Empty;
    public DateTime ConsentedAtUtc { get; set; }
    public DateTime? UnsubscribedAtUtc { get; set; }
}
