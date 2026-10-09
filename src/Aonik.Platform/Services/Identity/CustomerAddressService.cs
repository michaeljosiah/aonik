using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

using PartyEntity = Aonik.Platform.Entities.Party.Party;

namespace Aonik.Platform.Services.Identity;

internal sealed class CustomerAddressService(PlatformDbContext db, ITenantProvider tenantProvider,
    ICurrentUserProvider currentUser, ICurrentPartyResolver parties, IPermissionService permissions, IClock clock)
    : ICustomerAddressService
{
    public async Task<CustomerAddressBookDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        var owner = await OwnerAsync("UserInfo.Read", cancellationToken);
        if (owner is null) return null;
        var party = await db.Parties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == owner.Value.PartyId && p.TenantId == owner.Value.TenantId && !p.IsDeleted, cancellationToken);
        if (party is null) return null;
        var addresses = await AddressesAsync(party.Id, false, cancellationToken);
        return Map(party, addresses);
    }

    public Task<CustomerAddressBookDto> CreateAsync(CustomerAddressWrite command, CancellationToken cancellationToken = default)
        => WriteAsync(null, command, command.ExpectedVersion, Mutation.Create, cancellationToken);

    public Task<CustomerAddressBookDto> UpdateAsync(Guid addressId, CustomerAddressWrite command, CancellationToken cancellationToken = default)
        => WriteAsync(addressId, command, command.ExpectedVersion, Mutation.Update, cancellationToken);

    public Task<CustomerAddressBookDto> DeleteAsync(Guid addressId, string? expectedVersion, CancellationToken cancellationToken = default)
        => WriteAsync(addressId, null, expectedVersion, Mutation.Delete, cancellationToken);

    public Task<CustomerAddressBookDto> SetDefaultAsync(Guid addressId, string? expectedVersion, CancellationToken cancellationToken = default)
        => WriteAsync(addressId, null, expectedVersion, Mutation.SetDefault, cancellationToken);

    private async Task<CustomerAddressBookDto> WriteAsync(Guid? addressId, CustomerAddressWrite? command,
        string? expectedVersion, Mutation mutation, CancellationToken ct)
    {
        var owner = await OwnerAsync("UserInfo.Update", ct) ?? throw Missing();
        var createdId = Guid.NewGuid();
        byte[]? savedVersion = null;
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                Detach(owner.PartyId);
                await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
                var party = await db.Parties.SingleOrDefaultAsync(p => p.Id == owner.PartyId && p.TenantId == owner.TenantId && !p.IsDeleted, ct)
                    ?? throw Missing();
                var addresses = await AddressesAsync(party.Id, true, ct);
                // Save succeeded but its commit acknowledgement may have been lost. The native
                // parent version proves that exact save; the stable create ID prevents duplicates.
                if ((savedVersion is { Length: > 0 } && savedVersion.SequenceEqual(party.RowVersion))
                    || (mutation == Mutation.Create && addresses.Any(a => a.Id == createdId)))
                    return Map(party, addresses);

                var target = addressId is { } id ? addresses.SingleOrDefault(a => a.Id == id) ?? throw Missing() : null;
                RequireVersion(party, expectedVersion);
                var defaultId = DefaultId(party, addresses);
                if (command is not null)
                {
                    var normalized = CustomerAddressInputValidator.Normalize(command);
                    var validation = new CustomerAddressInputValidator().Validate(normalized);
                    if (!validation.IsValid) throw new InvalidStateException(string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));
                    if (mutation == Mutation.Create)
                    {
                        target = new PartyAddress { Id = createdId, PartyId = party.Id, CreatedAt = clock.UtcNow };
                        db.PartyAddresses.Add(target);
                        addresses.Add(target);
                    }
                    Apply(target!, normalized);
                }
                if (mutation == Mutation.Delete)
                {
                    db.PartyAddresses.Remove(target!);
                    addresses.Remove(target!);
                    if (defaultId == addressId) defaultId = null;
                }
                if (mutation == Mutation.SetDefault) defaultId = addressId;
                party.DefaultShippingAddressId = defaultId ?? Earliest(addresses)?.Id;
                party.UpdatedAt = clock.UtcNow;
                db.Entry(party).Property(p => p.UpdatedAt).IsModified = true;
                await db.SaveChangesAsync(ct);
                savedVersion = party.RowVersion.ToArray();
                if (transaction is not null) await transaction.CommitAsync(ct);
                return Map(party, addresses);
            });
        }
        catch
        {
            Detach(owner.PartyId);
            throw;
        }
    }

    private async Task<(Guid TenantId, Guid PartyId)?> OwnerAsync(string permission, CancellationToken ct)
    {
        if (!currentUser.TryGetCurrentUserId(out var userId) || userId == Guid.Empty)
            throw new UnauthorizedAccessException("Authentication required.");
        var tenantId = tenantProvider.GetCurrentTenantId();
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.TenantId == tenantId && !u.IsDeleted && u.Status == "Active", ct))
            return null;
        if (!await permissions.HasPermissionAsync(userId, permission, ct)) throw new PermissionDeniedException(permission);
        var partyId = await parties.GetCurrentPartyIdAsync(ct);
        if (partyId is null || !await db.Parties.AsNoTracking().AnyAsync(p => p.Id == partyId && p.TenantId == tenantId && !p.IsDeleted, ct)) return null;
        return (tenantId, partyId.Value);
    }

    private Task<List<PartyAddress>> AddressesAsync(Guid partyId, bool tracking, CancellationToken ct)
    {
        // PartyAddress is not tenant-scoped and Platform's tenant filter does not hide its soft deletes.
        var query = db.PartyAddresses.Where(a => a.PartyId == partyId && !a.IsDeleted);
        return (tracking ? query : query.AsNoTracking()).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).ToListAsync(ct);
    }

    private static CustomerAddressBookDto Map(PartyEntity party, IReadOnlyCollection<PartyAddress> addresses)
    {
        var defaultId = DefaultId(party, addresses);
        return new(addresses.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).Select(a => new CustomerAddressDto(
            a.Id, a.Type, a.Line1, a.Line2, a.Line3, a.City, a.State, a.Postcode, a.Country, a.Id == defaultId)).ToList(),
            defaultId, Convert.ToBase64String(party.RowVersion));
    }

    private static Guid? DefaultId(PartyEntity party, IReadOnlyCollection<PartyAddress> addresses)
        => addresses.Any(a => a.Id == party.DefaultShippingAddressId) ? party.DefaultShippingAddressId : Earliest(addresses)?.Id;

    private static PartyAddress? Earliest(IEnumerable<PartyAddress> addresses) => addresses.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).FirstOrDefault();

    private static void RequireVersion(PartyEntity party, string? version)
    {
        byte[]? bytes = null;
        try { if (version is { Length: <= 64 } && !version.Any(char.IsWhiteSpace)) bytes = Convert.FromBase64String(version); }
        catch (FormatException) { }
        if (bytes is null || !bytes.SequenceEqual(party.RowVersion))
            throw new DbUpdateConcurrencyException("The address book changed. Reload it before saving.");
    }

    private static void Apply(PartyAddress address, CustomerAddressWrite command)
    {
        address.Type = command.Type; address.Line1 = command.Line1; address.Line2 = command.Line2; address.Line3 = command.Line3;
        address.City = command.City; address.State = command.State; address.Postcode = command.Postcode; address.Country = command.Country;
    }

    private void Detach(Guid partyId)
    {
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.Entity is PartyEntity p && p.Id == partyId
            || e.Entity is PartyAddress a && a.PartyId == partyId).ToArray()) entry.State = EntityState.Detached;
    }

    private static NotFoundException Missing() => new("Customer address not found.");
    private enum Mutation { Create, Update, Delete, SetDefault }
}
