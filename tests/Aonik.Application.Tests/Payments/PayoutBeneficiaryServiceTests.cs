using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Payments;
using Aonik.SharedKernel.Abstractions;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Payments;

/// <summary>
/// Tests for <see cref="PayoutBeneficiaryService"/>. The service stitches a payout destination
/// together with the customer→recipient party graph through the cross-module <see cref="IPartyService"/>
/// seam. A stateful fake (not a strict mock) backs the seam so these tests can assert the observable
/// contract: exactly one relationship edge and one Beneficiary role per recipient (idempotent across
/// rails), and that saved destinations come back through the list path.
/// </summary>
public class PayoutBeneficiaryServiceTests
{
    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"PayoutBeneficiaryTests_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(
            options,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider());
    }

    private static PayoutBeneficiaryService CreateService(
        FinanceDbContext context,
        Guid tenantId,
        FakePartyService partyService)
        => new(context, partyService, new TestTenantProvider(tenantId));

    [Fact]
    public async Task SaveBeneficiaryAsync_Should_CreateRecipientPartyEdgeRoleAndAccount_When_NewBeneficiary()
    {
        // Arrange — a customer with no saved beneficiaries yet.
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var party = new FakePartyService();
        var customerId = party.SeedParty("John Customer");
        var service = CreateService(context, tenantId, party);

        var request = new SavePayoutBeneficiaryRequest(
            CustomerPartyId: customerId,
            DestinationType: "Bank",
            AccountName: "Jane Doe",
            Currency: "ngn",
            MaskedAccountIdentifier: "****1234",
            BankCode: "044");

        // Act
        var result = await service.SaveBeneficiaryAsync(request);

        // Assert — response is well-formed and normalized.
        result.BeneficiaryPartyId.Should().NotBeEmpty();
        result.CustomerPartyId.Should().Be(customerId);
        result.BeneficiaryName.Should().Be("Jane Doe");
        result.Currency.Should().Be("NGN");
        result.DestinationType.Should().Be("Bank");
        result.BankCode.Should().Be("044");
        result.RelationshipTypeCode.Should().Be(PartyRelationshipTypeCodes.Recipient);
        result.IsVerified.Should().BeFalse();

        // Exactly one customer→recipient Recipient edge was created.
        party.Relationships.Should().ContainSingle()
            .Which.Should().BeEquivalentTo((customerId, result.BeneficiaryPartyId, PartyRelationshipTypeCodes.Recipient));

        // The recipient was marked payable as a Beneficiary scoped to this customer.
        party.Roles.Should().ContainSingle()
            .Which.Should().BeEquivalentTo((result.BeneficiaryPartyId, PartyRoleCodes.Beneficiary, "Customer", customerId));

        // The structured destination was persisted under the tenant.
        var account = await context.ExternalPayoutAccounts.SingleAsync();
        account.TenantId.Should().Be(tenantId);
        account.BeneficiaryPartyId.Should().Be(result.BeneficiaryPartyId);
        account.MaskedAccountIdentifier.Should().Be("****1234");
        account.Currency.Should().Be("NGN");
    }

    [Fact]
    public async Task SaveBeneficiaryAsync_Should_NotDuplicateEdgeOrRole_When_SavingSecondRailForSameRecipient()
    {
        // Arrange — an existing recipient party; the customer saves two different rails for them.
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var party = new FakePartyService();
        var customerId = party.SeedParty("John Customer");
        var recipientId = party.SeedParty("Jane Doe");
        var service = CreateService(context, tenantId, party);

        var bankRail = new SavePayoutBeneficiaryRequest(
            CustomerPartyId: customerId,
            DestinationType: "Bank",
            AccountName: "Jane Doe",
            Currency: "NGN",
            MaskedAccountIdentifier: "****1234",
            BankCode: "044",
            BeneficiaryPartyId: recipientId);

        var mobileRail = new SavePayoutBeneficiaryRequest(
            CustomerPartyId: customerId,
            DestinationType: "MobileMoney",
            AccountName: "Jane Doe",
            Currency: "NGN",
            MaskedAccountIdentifier: "****6789",
            MobileNetwork: "MTN",
            BeneficiaryPartyId: recipientId);

        // Act — save both rails for the same recipient.
        await service.SaveBeneficiaryAsync(bankRail);
        await service.SaveBeneficiaryAsync(mobileRail);

        // Assert — the party graph is deduped: one edge, one role; but both rails persist.
        party.Relationships.Should().ContainSingle();
        party.Roles.Should().ContainSingle();

        var accounts = await context.ExternalPayoutAccounts.ToListAsync();
        accounts.Should().HaveCount(2);
        accounts.Should().OnlyContain(account => account.BeneficiaryPartyId == recipientId);
        accounts.Select(account => account.DestinationType).Should().BeEquivalentTo(new[] { "Bank", "MobileMoney" });
    }

    [Fact]
    public async Task ListBeneficiariesAsync_Should_ReturnSavedBeneficiaries()
    {
        // Arrange — a customer saves two distinct beneficiaries.
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var party = new FakePartyService();
        var customerId = party.SeedParty("John Customer");
        var service = CreateService(context, tenantId, party);

        await service.SaveBeneficiaryAsync(new SavePayoutBeneficiaryRequest(
            CustomerPartyId: customerId,
            DestinationType: "Bank",
            AccountName: "Jane Doe",
            Currency: "NGN",
            MaskedAccountIdentifier: "****1234",
            BankCode: "044"));

        await service.SaveBeneficiaryAsync(new SavePayoutBeneficiaryRequest(
            CustomerPartyId: customerId,
            DestinationType: "MobileMoney",
            AccountName: "Acme Power",
            Currency: "NGN",
            MaskedAccountIdentifier: "****6789",
            MobileNetwork: "MTN"));

        // Act
        var result = await service.ListBeneficiariesAsync(customerId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(item => item.CustomerPartyId == customerId);
        result.Select(item => item.BeneficiaryName).Should().BeEquivalentTo(new[] { "Jane Doe", "Acme Power" });
        result.Select(item => item.MaskedAccountIdentifier).Should().BeEquivalentTo(new[] { "****1234", "****6789" });
    }

    [Fact]
    public async Task ListBeneficiariesAsync_Should_ReturnEmpty_When_CustomerHasNoRelationships()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var party = new FakePartyService();
        var customerId = party.SeedParty("John Customer");
        var service = CreateService(context, tenantId, party);

        var result = await service.ListBeneficiariesAsync(customerId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveBeneficiaryAsync_Should_Throw_When_SuppliedBeneficiaryPartyNotFound()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var party = new FakePartyService();
        var customerId = party.SeedParty("John Customer");
        var service = CreateService(context, tenantId, party);

        var request = new SavePayoutBeneficiaryRequest(
            CustomerPartyId: customerId,
            DestinationType: "Bank",
            AccountName: "Jane Doe",
            Currency: "NGN",
            MaskedAccountIdentifier: "****1234",
            BeneficiaryPartyId: Guid.NewGuid());

        await service.Invoking(s => s.SaveBeneficiaryAsync(request))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Stateful in-memory <see cref="IPartyService"/>: stores parties, relationship edges, and role
    /// assignments so tests can assert what the service wrote across the seam. Mirrors the real
    /// implementation's idempotent role assignment; relationship inserts are recorded verbatim so the
    /// service's own find-or-create dedupe is what's under test.
    /// </summary>
    private sealed class FakePartyService : IPartyService
    {
        private readonly Dictionary<Guid, PartyResponse> _parties = new();

        public List<(Guid From, Guid To, string Type)> Relationships { get; } = new();

        public List<(Guid PartyId, string Role, string ContextType, Guid ContextId)> Roles { get; } = new();

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
            Relationships.Add((request.FromPartyId, request.ToPartyId, request.RelationshipTypeCode));
            return Task.FromResult(BuildRelationship(request.FromPartyId, request.ToPartyId, request.RelationshipTypeCode));
        }

        public Task<IReadOnlyList<PartyRelationshipResponse>> GetRelationshipsAsync(Guid partyId, CancellationToken cancellationToken = default)
        {
            var list = Relationships
                .Where(relationship => relationship.From == partyId || relationship.To == partyId)
                .Select(relationship => BuildRelationship(relationship.From, relationship.To, relationship.Type))
                .ToList();

            return Task.FromResult<IReadOnlyList<PartyRelationshipResponse>>(list);
        }

        public Task AssignPartyRoleAsync(Guid partyId, string role, string contextType, Guid contextId, CancellationToken cancellationToken = default)
        {
            var alreadyAssigned = Roles.Any(assignment =>
                assignment.PartyId == partyId
                && assignment.Role == role
                && assignment.ContextType == contextType
                && assignment.ContextId == contextId);

            if (!alreadyAssigned)
            {
                Roles.Add((partyId, role, contextType, contextId));
            }

            return Task.CompletedTask;
        }

        private PartyRelationshipResponse BuildRelationship(Guid fromPartyId, Guid toPartyId, string typeCode)
            => new(
                Guid.NewGuid(),
                fromPartyId,
                _parties.TryGetValue(fromPartyId, out var from) ? from.DisplayName : string.Empty,
                toPartyId,
                _parties.TryGetValue(toPartyId, out var to) ? to.DisplayName : string.Empty,
                typeCode,
                typeCode,
                true);
    }
}
