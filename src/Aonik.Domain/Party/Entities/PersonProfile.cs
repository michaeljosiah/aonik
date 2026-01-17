using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Party.Entities;

public class PersonProfile : AuditableEntity
{
    public Guid PartyId { get; set; }
    public string? Title { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? CountryCode { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTime? Dob { get; set; }
    public string? Nationality { get; set; }
    public string? Occupation { get; set; }
    public string IdvStatus { get; set; } = string.Empty;
    public Party Party { get; set; } = null!;
}
