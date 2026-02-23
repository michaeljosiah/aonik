using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Party;

public class PartyContact : AuditableEntity
{
    public Guid PartyId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
