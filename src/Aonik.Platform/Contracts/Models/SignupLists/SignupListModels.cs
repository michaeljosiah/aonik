namespace Aonik.Platform.Contracts.Models.SignupLists;

public static class SignupListTypes
{
    public const string Newsletter = "newsletter";
    public const string DeliveryAvailability = "delivery-availability";
    public const string PrivateTable = "private-table";

    public static bool IsKnown(string value)
        => value is Newsletter or DeliveryAvailability or PrivateTable;
}

public record SignupListsConfigurationDto(List<SignupListDefinitionDto> Lists);

public record SignupListDefinitionDto(
    string ListType,
    string ConsentVersion,
    string ConsentText,
    List<SignupServiceOptionDto>? Services = null);

public record SignupServiceOptionDto(string Id, string Label);

public record SignupCaptureRequest(
    string Email,
    string ConsentVersion,
    string? Postcode = null,
    string? Name = null,
    string? Phone = null,
    string? Country = null,
    string? Service = null);

public record SignupSubscriptionDto(
    Guid Id,
    string ListType,
    string Email,
    string? Postcode,
    string? PostcodeOutwardCode,
    string? Name,
    string? Phone,
    string? Country,
    string? Service,
    string ConsentVersion,
    string ConsentText,
    string ConsentSource,
    DateTime ConsentedAtUtc,
    DateTime? UnsubscribedAtUtc,
    string? UnsubscribeToken);

public record SignupAreaDemandDto(string PostcodeOutwardCode, int SubscriberCount);
