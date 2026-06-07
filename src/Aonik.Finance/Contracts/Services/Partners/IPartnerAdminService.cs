using Aonik.SharedKernel.Abstractions;
using Aonik.Finance.Contracts.Models.Partners;

namespace Aonik.Finance.Contracts.Services.Partners;

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

    Task<PartnerDetail> CreateConnectorAsync(
        Guid partnerId,
        CreatePartnerConnectorRequest request,
        CancellationToken cancellationToken = default);

    Task<PartnerDetail> UpdateConnectorAsync(
        Guid partnerId,
        Guid connectorId,
        UpdatePartnerConnectorRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteConnectorAsync(
        Guid partnerId,
        Guid connectorId,
        CancellationToken cancellationToken = default);
}
