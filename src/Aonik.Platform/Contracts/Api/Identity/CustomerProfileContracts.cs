namespace Aonik.Platform.Contracts.Api.Identity;

public record UpdateCustomerProfileRequest(
    string? FirstName,
    string? LastName,
    string? Title,
    string? Phone,
    string? CountryCode);

public record UpdateCustomerEmailRequest(
    string CurrentEmail,
    string NewEmail,
    string Password);

public record UpdateCustomerPasswordRequest(
    string CurrentPassword,
    string NewPassword);

public record UpdateCustomerPasswordResponse(
    string Status);

public record CustomerPhotoUploadResponse(
    string PhotoUrl);

public record CustomerPhotoDeleteResponse(
    string Status);

public record CustomerProfileResponse(
    Guid PartyId,
    Guid UserId,
    Guid TenantId,
    string Email,
    string? FirstName,
    string? LastName,
    string? Title,
    string? Phone,
    string? CountryCode,
    string? PhotoUrl);

public record NotificationPreferencesResponse(
    string Email,
    bool NewBillsPush,
    bool BillUpdatesPush,
    bool BillAssistPush,
    bool MbaMessagesPush,
    bool OrgMessagesPush,
    bool FriendsMessagesPush,
    bool NewBillsEmail,
    bool BillUpdatesEmail,
    bool BillAssistEmail,
    bool MbaMessagesEmail,
    bool OrgMessagesEmail);

public record UpdateNotificationPreferencesRequest(
    string? Email,
    bool NewBillsPush,
    bool BillUpdatesPush,
    bool BillAssistPush,
    bool MbaMessagesPush,
    bool OrgMessagesPush,
    bool FriendsMessagesPush,
    bool NewBillsEmail,
    bool BillUpdatesEmail,
    bool BillAssistEmail,
    bool MbaMessagesEmail,
    bool OrgMessagesEmail);

public record MarketingPreferencesResponse(
    string Email,
    bool News,
    bool Offers,
    bool Surveys);

public record UpdateMarketingPreferencesRequest(
    string? Email,
    bool News,
    bool Offers,
    bool Surveys);
