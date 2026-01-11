namespace Aonik.Application.Models.Identity;

public record UserProvisioningResult(
    Guid UserId,
    Guid PartyId,
    bool UserCreated,
    bool PartyCreated,
    bool LinkCreated);
