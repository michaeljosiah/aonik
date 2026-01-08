using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Party.Entities;

public class PersonProfile : AuditableEntity
{
    public Guid PartyId { get; private set; }
    public DateTime? Dob { get; private set; }
    public string? Nationality { get; private set; }
    public string? Occupation { get; private set; }
    public string IdvStatus { get; private set; } = string.Empty;

    public Party Party { get; private set; } = null!;

    private PersonProfile() { }

    public PersonProfile(Guid partyId, DateTime? dob = null, string? nationality = null, string? occupation = null)
    {
        PartyId = partyId;
        Dob = dob;
        Nationality = nationality;
        Occupation = occupation;
        IdvStatus = "Pending";
    }

    public void UpdateProfile(DateTime? dob, string? nationality, string? occupation)
    {
        Dob = dob;
        Nationality = nationality;
        Occupation = occupation;
    }

    public void UpdateIdvStatus(string status)
    {
        IdvStatus = status;
    }
}
