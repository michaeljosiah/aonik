using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Party;

public class PartyAddress : AuditableEntity
{
    public Guid PartyId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string? Line3 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
    public string Postcode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
