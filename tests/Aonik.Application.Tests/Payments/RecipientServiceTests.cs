using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Payments;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Persistence;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Payments;

/// <summary>
/// Tests for <see cref="RecipientService"/>, the customer-facing façade over the payout-beneficiary
/// party graph. The service composes the shipped <see cref="PayoutBeneficiaryService"/> with the
/// cross-module <see cref="IPartyService"/> seam, so a stateful fake party service (with stable edge
/// ids, active tracking, and photos) backs the seam while the real <see cref="PayoutBeneficiaryService"/>
/// and an in-memory <see cref="FinanceDbContext"/> exercise the rail/storage path.
/// </summary>
public class RecipientServiceTests
{
    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"RecipientTests_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(options, new TestTenantProvider(tenantId), new TestCurrentUserProvider());
    }

    private static (RecipientService Recipients, FakePartyService Party, FinanceDbContext Context) CreateService(
        Guid tenantId, FinanceDbContext context)
    {
        var party = new FakePartyService();
        var tenant = new TestTenantProvider(tenantId);
        var beneficiary = new PayoutBeneficiaryService(context, party, tenant);
        var recipients = new RecipientService(context, party, beneficiary, tenant);
        return (recipients, party, context);
    }

    private static SavePayoutBeneficiaryRequest BankRail(Guid customerId, string name, string masked, string bankCode = "044")
        => new(
            CustomerPartyId: customerId,
            DestinationType: "Bank",
            AccountName: name,
            Currency: "NGN",
            MaskedAccountIdentifier: masked,
            BankCode: bankCode);

    // A rail for an explicitly-shared beneficiary party — two customers pointing at the same payee.
    private static SavePayoutBeneficiaryRequest SharedRail(Guid customerId, Guid sharedPayeePartyId, string masked, string bankCode)
        => new(
            CustomerPartyId: customerId,
            DestinationType: "Bank",
            AccountName: "Shared Payee",
            Currency: "NGN",
            MaskedAccountIdentifier: masked,
            BankCode: bankCode,
            BeneficiaryPartyId: sharedPayeePartyId);

    [Fact]
    public async Task CreateAsync_Should_ProjectRecipientWithRail_When_NewRecipient()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (recipients, party, _) = CreateService(tenantId, context);
        var customerId = party.SeedParty("John Customer");

        var recipient = await recipients.CreateAsync(BankRail(customerId, "Jane Doe", "****1234"));

        recipient.DisplayName.Should().Be("Jane Doe");
        recipient.RelationshipTypeCode.Should().Be(PartyRelationshipTypeCodes.Recipient);
        recipient.IsActive.Should().BeTrue();
        recipient.Rails.Should().ContainSingle();
        recipient.Rails[0].MaskedAccountIdentifier.Should().Be("****1234");
        recipient.Rails[0].Currency.Should().Be("NGN");
        recipient.Rails[0].DestinationType.Should().Be("Bank");
    }

    [Fact]
    public async Task ListAsync_Should_FilterBySearch_AndPage()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (recipients, party, _) = CreateService(tenantId, context);
        var customerId = party.SeedParty("John Customer");

        await recipients.CreateAsync(BankRail(customerId, "Jane Doe", "****1"));
        await recipients.CreateAsync(BankRail(customerId, "Janet Smith", "****2", bankCode: "058"));
        await recipients.CreateAsync(BankRail(customerId, "Acme Power", "****3", bankCode: "011"));

        var all = await recipients.ListAsync(customerId, new RecipientQuery());
        all.TotalCount.Should().Be(3);
        all.Recipients.Should().HaveCount(3);

        var search = await recipients.ListAsync(customerId, new RecipientQuery(Search: "jan"));
        search.TotalCount.Should().Be(2);
        search.Recipients.Select(r => r.DisplayName).Should().BeEquivalentTo(new[] { "Jane Doe", "Janet Smith" });

        var firstPage = await recipients.ListAsync(customerId, new RecipientQuery(Page: 1, PageSize: 2));
        firstPage.Recipients.Should().HaveCount(2);
        firstPage.TotalCount.Should().Be(3);
        firstPage.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_Should_ReturnNull_When_RecipientNotOwnedByCustomer()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (recipients, party, _) = CreateService(tenantId, context);
        var customerId = party.SeedParty("John Customer");
        var otherCustomerId = party.SeedParty("Other Customer");

        var created = await recipients.CreateAsync(BankRail(customerId, "Jane Doe", "****1234"));

        (await recipients.GetAsync(otherCustomerId, created.RecipientPartyId)).Should().BeNull();
        (await recipients.GetAsync(customerId, created.RecipientPartyId)).Should().NotBeNull();
    }

    [Fact]
    public async Task RemoveAsync_Should_SoftDeleteRails_DeactivateEdge_AndPreserveHistoricalOrder()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (recipients, party, _) = CreateService(tenantId, context);
        var customerId = party.SeedParty("John Customer");

        var created = await recipients.CreateAsync(BankRail(customerId, "Jane Doe", "****1234"));
        var recipientId = created.RecipientPartyId;

        // A historical order that references the recipient party (soft Guid reference, no FK).
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderType = "Remittance",
            Status = "Completed",
            CurrencyIn = "GBP",
            AmountIn = 100m,
            PayerPartyId = recipientId
        };
        context.Set<Order>().Add(order);
        await context.SaveChangesAsync();

        await recipients.RemoveAsync(customerId, recipientId);

        // Recipient is gone from the payable surface.
        (await recipients.GetAsync(customerId, recipientId)).Should().BeNull();
        (await recipients.ListAsync(customerId, new RecipientQuery())).Recipients.Should().BeEmpty();

        // The rail is soft-deleted (IsDeleted = true), not hard-deleted.
        var rails = await context.ExternalPayoutAccounts
            .IncludeSoftDeleted()
            .Where(account => account.BeneficiaryPartyId == recipientId)
            .ToListAsync();
        rails.Should().ContainSingle().Which.IsDeleted.Should().BeTrue();

        // The owning Recipient edge is deactivated.
        var edge = (await party.GetRelationshipsAsync(customerId)).Single(e => e.ToPartyId == recipientId);
        edge.IsActive.Should().BeFalse();

        // The historical order is untouched.
        var persistedOrder = await context.Set<Order>().FirstOrDefaultAsync(o => o.Id == order.Id);
        persistedOrder.Should().NotBeNull();
        persistedOrder!.PayerPartyId.Should().Be(recipientId);
    }

    [Fact]
    public async Task UploadPhotoAsync_Should_StoreUrls_AndSurfaceOnProjection_When_RecipientOwned()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (recipients, party, _) = CreateService(tenantId, context);
        var customerId = party.SeedParty("John Customer");

        var created = await recipients.CreateAsync(BankRail(customerId, "Jane Doe", "****1234"));

        using var photoStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var photo = await recipients.UploadPhotoAsync(customerId, created.RecipientPartyId, "image/jpeg", photoStream);

        photo.RecipientPartyId.Should().Be(created.RecipientPartyId);
        photo.PhotoUrl.Should().NotBeNullOrEmpty();

        var refreshed = await recipients.GetAsync(customerId, created.RecipientPartyId);
        refreshed!.PhotoUrl.Should().Be(photo.PhotoUrl);
    }

    [Fact]
    public async Task UploadPhotoAsync_Should_Throw_When_RecipientNotOwnedByCustomer()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (recipients, party, _) = CreateService(tenantId, context);
        var customerId = party.SeedParty("John Customer");
        var otherCustomerId = party.SeedParty("Other Customer");

        var created = await recipients.CreateAsync(BankRail(customerId, "Jane Doe", "****1234"));

        using var photoStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await recipients
            .Invoking(s => s.UploadPhotoAsync(otherCustomerId, created.RecipientPartyId, "image/jpeg", photoStream))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ListAsync_Should_NotLeakAnotherCustomersRails_When_RecipientPartyShared()
    {
        // Two customers in the same tenant save a rail for the SAME payee party.
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (recipients, party, _) = CreateService(tenantId, context);
        var customerA = party.SeedParty("Customer A");
        var customerB = party.SeedParty("Customer B");
        var sharedPayee = party.SeedParty("Shared Payee");

        await recipients.CreateAsync(SharedRail(customerA, sharedPayee, "****AAAA", "044"));
        await recipients.CreateAsync(SharedRail(customerB, sharedPayee, "****BBBB", "058"));

        // Each customer sees only their own rail for the shared payee — never the other's.
        var aList = await recipients.ListAsync(customerA, new RecipientQuery());
        aList.Recipients.Should().ContainSingle()
            .Which.Rails.Should().ContainSingle().Which.MaskedAccountIdentifier.Should().Be("****AAAA");

        var bList = await recipients.ListAsync(customerB, new RecipientQuery());
        bList.Recipients.Should().ContainSingle()
            .Which.Rails.Should().ContainSingle().Which.MaskedAccountIdentifier.Should().Be("****BBBB");
    }

    [Fact]
    public async Task RemoveAsync_Should_OnlyRemoveOwnRails_When_RecipientPartySharedAcrossCustomers()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (recipients, party, _) = CreateService(tenantId, context);
        var customerA = party.SeedParty("Customer A");
        var customerB = party.SeedParty("Customer B");
        var sharedPayee = party.SeedParty("Shared Payee");

        await recipients.CreateAsync(SharedRail(customerA, sharedPayee, "****AAAA", "044"));
        await recipients.CreateAsync(SharedRail(customerB, sharedPayee, "****BBBB", "058"));

        // Customer A removes the shared payee.
        await recipients.RemoveAsync(customerA, sharedPayee);

        // A no longer sees the recipient; B is entirely unaffected.
        (await recipients.GetAsync(customerA, sharedPayee)).Should().BeNull();
        var bView = await recipients.GetAsync(customerB, sharedPayee);
        bView.Should().NotBeNull();
        bView!.Rails.Should().ContainSingle().Which.MaskedAccountIdentifier.Should().Be("****BBBB");

        // Exactly A's rail is soft-deleted; B's remains live.
        var rails = await context.ExternalPayoutAccounts
            .IncludeSoftDeleted()
            .Where(account => account.BeneficiaryPartyId == sharedPayee)
            .ToListAsync();
        rails.Should().HaveCount(2);
        rails.Single(account => account.CustomerPartyId == customerA).IsDeleted.Should().BeTrue();
        rails.Single(account => account.CustomerPartyId == customerB).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAsync_Should_NotTouchRails_When_CustomerDoesNotOwnRecipient()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (recipients, party, _) = CreateService(tenantId, context);
        var owner = party.SeedParty("Owner");
        var stranger = party.SeedParty("Stranger");

        var created = await recipients.CreateAsync(BankRail(owner, "Jane Doe", "****1234"));
        var recipientId = created.RecipientPartyId;

        // A stranger with no edge to the recipient attempts removal by supplying the party id.
        await recipients.RemoveAsync(stranger, recipientId);

        // The owner's rail and recipient are untouched — nothing was deleted.
        var ownerView = await recipients.GetAsync(owner, recipientId);
        ownerView.Should().NotBeNull();
        ownerView!.Rails.Should().ContainSingle();

        var rails = await context.ExternalPayoutAccounts
            .IncludeSoftDeleted()
            .Where(account => account.BeneficiaryPartyId == recipientId)
            .ToListAsync();
        rails.Should().ContainSingle().Which.IsDeleted.Should().BeFalse();
    }

    /// <summary>
    /// Stateful in-memory <see cref="IPartyService"/> for recipient tests. Edges carry a stable id and an
    /// active flag so the service can round-trip them (update / deactivate); photos are stored so the
    /// projection can surface them. Mirrors the real implementation's idempotent role assignment.
    /// </summary>
    private sealed class FakePartyService : IPartyService
    {
        private sealed class Edge
        {
            public Guid Id { get; init; }
            public Guid From { get; init; }
            public Guid To { get; init; }
            public string Type { get; set; } = string.Empty;
            public string? Notes { get; set; }
            public bool IsActive { get; set; } = true;
        }

        private readonly Dictionary<Guid, PartyResponse> _parties = new();
        private readonly List<Edge> _edges = new();
        private readonly List<(Guid PartyId, string Role, string ContextType, Guid ContextId)> _roles = new();
        private readonly Dictionary<Guid, PartyPhotoUrls> _photos = new();

        public Guid SeedParty(string displayName, string partyType = "Person")
        {
            var id = Guid.NewGuid();
            _parties[id] = new PartyResponse(id, displayName, partyType, "Active");
            return id;
        }

        public Task<PartyResponse> CreatePartyAsync(CreatePartyRequest request, CancellationToken cancellationToken = default)
        {
            var party = new PartyResponse(Guid.NewGuid(), request.DisplayName, request.PartyType, "Active");
            _parties[party.PartyId] = party;
            return Task.FromResult(party);
        }

        public Task<PartyResponse?> GetPartyAsync(Guid partyId, CancellationToken cancellationToken = default)
            => Task.FromResult(_parties.TryGetValue(partyId, out var party) ? party : null);

        public Task<RelatedPartyResponse> CreateRelatedPartyAsync(CreateRelatedPartyRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PartyRelationshipResponse> CreateRelationshipAsync(CreatePartyRelationshipRequest request, CancellationToken cancellationToken = default)
        {
            var edge = new Edge
            {
                Id = Guid.NewGuid(),
                From = request.FromPartyId,
                To = request.ToPartyId,
                Type = request.RelationshipTypeCode,
                Notes = request.Notes
            };
            _edges.Add(edge);
            return Task.FromResult(ToResponse(edge));
        }

        public Task<IReadOnlyList<PartyRelationshipResponse>> GetRelationshipsAsync(Guid partyId, CancellationToken cancellationToken = default)
        {
            var list = _edges
                .Where(edge => edge.From == partyId || edge.To == partyId)
                .Select(ToResponse)
                .ToList();

            return Task.FromResult<IReadOnlyList<PartyRelationshipResponse>>(list);
        }

        public Task AssignPartyRoleAsync(Guid partyId, string role, string contextType, Guid contextId, CancellationToken cancellationToken = default)
        {
            if (!_roles.Any(r => r.PartyId == partyId && r.Role == role && r.ContextType == contextType && r.ContextId == contextId))
            {
                _roles.Add((partyId, role, contextType, contextId));
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PartyPhotoUrls>> GetPartyPhotosAsync(IReadOnlyCollection<Guid> partyIds, CancellationToken cancellationToken = default)
        {
            var list = partyIds
                .Where(_photos.ContainsKey)
                .Select(id => _photos[id])
                .ToList();

            return Task.FromResult<IReadOnlyList<PartyPhotoUrls>>(list);
        }

        public Task<PartyPhotoUrls> SetPartyPhotoAsync(Guid partyId, string contentType, Stream photo, CancellationToken cancellationToken = default)
        {
            var urls = new PartyPhotoUrls(
                partyId,
                $"https://blob.test/{partyId:N}/photo.jpg",
                $"https://blob.test/{partyId:N}/photo_512.jpg",
                $"https://blob.test/{partyId:N}/photo_128.jpg",
                $"https://blob.test/{partyId:N}/photo_64.jpg");

            _photos[partyId] = urls;
            return Task.FromResult(urls);
        }

        public Task<bool> UpdateRelationshipAsync(Guid relationshipId, string? relationshipTypeCode = null, string? notes = null, bool? isActive = null, CancellationToken cancellationToken = default)
        {
            var edge = _edges.FirstOrDefault(e => e.Id == relationshipId);
            if (edge is null)
            {
                return Task.FromResult(false);
            }

            if (!string.IsNullOrWhiteSpace(relationshipTypeCode))
            {
                edge.Type = relationshipTypeCode.Trim();
            }

            if (notes is not null)
            {
                edge.Notes = notes;
            }

            if (isActive.HasValue)
            {
                edge.IsActive = isActive.Value;
            }

            return Task.FromResult(true);
        }

        private PartyRelationshipResponse ToResponse(Edge edge)
            => new(
                edge.Id,
                edge.From,
                Name(edge.From),
                edge.To,
                Name(edge.To),
                edge.Type,
                edge.Type,
                edge.IsActive);

        private string Name(Guid id) => _parties.TryGetValue(id, out var party) ? party.DisplayName : string.Empty;
    }
}
