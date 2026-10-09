using System.Text.Json.Serialization;

using Aonik.SharedKernel.Validation;

namespace Aonik.Platform.Contracts.Models.Identity;

public static class AccountAccessPurposes
{
    public const string PaidSetup = "PaidSetup";
    public const string EmailChange = "EmailChange";
}

[NoValidation("The service bounds and verifies the opaque token; malformed, expired and unknown values must share the same neutral response.")]
public record AccountAccessTokenRequest(string? Token);
public record AccountAccessResponse(string Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record AccountAccessConfiguration(
    [property: JsonRequired] bool IsEnabled,
    [property: JsonRequired] string StorefrontOrigin,
    [property: JsonRequired] string SetupPath,
    [property: JsonRequired] string EmailChangePath);
