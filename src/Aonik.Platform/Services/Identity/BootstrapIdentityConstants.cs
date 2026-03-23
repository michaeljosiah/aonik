namespace Aonik.Platform.Services.Identity;

internal static class BootstrapIdentityConstants
{
    public const string PendingOwnerIssuer = "aonik-bootstrap";

    public static string CreatePendingOwnerSubject(string email)
        => $"owner:{email.Trim().ToLowerInvariant()}";
}
