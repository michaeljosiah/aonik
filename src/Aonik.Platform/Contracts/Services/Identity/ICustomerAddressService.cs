using Aonik.Platform.Contracts.Models.Identity;

namespace Aonik.Platform.Contracts.Services.Identity;

public interface ICustomerAddressService
{
    Task<CustomerAddressBookDto?> GetAsync(CancellationToken cancellationToken = default);
    Task<CustomerAddressBookDto> CreateAsync(CustomerAddressWrite command, CancellationToken cancellationToken = default);
    Task<CustomerAddressBookDto> UpdateAsync(Guid addressId, CustomerAddressWrite command, CancellationToken cancellationToken = default);
    Task<CustomerAddressBookDto> DeleteAsync(Guid addressId, string? expectedVersion, CancellationToken cancellationToken = default);
    Task<CustomerAddressBookDto> SetDefaultAsync(Guid addressId, string? expectedVersion, CancellationToken cancellationToken = default);
}
