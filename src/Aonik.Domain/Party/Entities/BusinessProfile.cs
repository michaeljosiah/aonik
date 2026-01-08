using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Party.Entities;

public class BusinessProfile : AuditableEntity
{
    public Guid PartyId { get; private set; }
    public string? RegistrationNumber { get; private set; }
    public string? IncorporationCountry { get; private set; }
    public string? Industry { get; private set; }
    public string KybStatus { get; private set; } = string.Empty;

    public Party Party { get; private set; } = null!;

    private BusinessProfile() { }

    public BusinessProfile(Guid partyId, string? registrationNumber = null, string? incorporationCountry = null, string? industry = null)
    {
        PartyId = partyId;
        RegistrationNumber = registrationNumber;
        IncorporationCountry = incorporationCountry;
        Industry = industry;
        KybStatus = "Pending";
    }

    public void UpdateProfile(string? registrationNumber, string? incorporationCountry, string? industry)
    {
        RegistrationNumber = registrationNumber;
        IncorporationCountry = incorporationCountry;
        Industry = industry;
    }

    public void UpdateKybStatus(string status)
    {
        KybStatus = status;
    }
}
