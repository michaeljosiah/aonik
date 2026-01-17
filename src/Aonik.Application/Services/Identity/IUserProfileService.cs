using Aonik.Application.Models.Identity;

namespace Aonik.Application.Services.Identity;

public interface IUserProfileService
{
    Task<CurrentUserSnapshot?> GetCurrentUserAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<CustomerProfileResponse?> GetCustomerProfileAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<CustomerProfileResponse?> UpdateCustomerProfileAsync(
        Guid userId,
        Guid tenantId,
        UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerProfileResponse?> UpdateCustomerEmailAsync(
        Guid userId,
        Guid tenantId,
        UpdateCustomerEmailRequest request,
        CancellationToken cancellationToken = default);

    Task<UpdateCustomerPasswordResponse> UpdateCustomerPasswordAsync(
        Guid userId,
        Guid tenantId,
        UpdateCustomerPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerPhotoUploadResponse?> UploadCustomerPhotoAsync(
        Guid userId,
        Guid tenantId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<CustomerPhotoDeleteResponse?> DeleteCustomerPhotoAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
