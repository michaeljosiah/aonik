using Aonik.Application.Models.Identity;
using Aonik.Application.Models.Partners;

namespace Aonik.Application.Services.Partners;

public interface IPartnerAdminService
{
    Task<PagedResult<PartnerListItem>> ListPartnersAsync(
        ListPartnersRequest request,
        CancellationToken cancellationToken = default);

    Task<PartnerDetail?> GetPartnerAsync(
        Guid partnerId,
        CancellationToken cancellationToken = default);

    Task<CreatePartnerResponse> CreatePartnerAsync(
        CreatePartnerRequest request,
        CancellationToken cancellationToken = default);

    Task<PartnerDetail> UpdatePartnerAsync(
        Guid partnerId,
        UpdatePartnerRequest request,
        CancellationToken cancellationToken = default);

    Task DeletePartnerAsync(
        Guid partnerId,
        CancellationToken cancellationToken = default);
}
