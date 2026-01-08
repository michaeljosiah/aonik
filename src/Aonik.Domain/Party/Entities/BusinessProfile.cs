using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Party.Entities;

public class BusinessProfile : AuditableEntity
{
    public Guid PartyId { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? IncorporationCountry { get; set; }
    public string? Industry { get; set; }
    public string KybStatus { get; set; } = string.Empty;
    public Party Party { get; set; } = null!;
}
