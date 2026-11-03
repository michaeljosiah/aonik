using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.Finance.Entities;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Payments;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Primitives;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

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
    private static readonly Guid CallerUserId = Guid.NewGuid();

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; init; } = new(2026, 6, 7, 12, 0, 0, DateTimeKind.Utc);
    }

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"PayoutBeneficiaryTests_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(
            options,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(CallerUserId));
    }

    private static PayoutBeneficiaryService CreateService(
        FinanceDbContext context,
        Guid tenantId,
        FakePartyService partyService,
        IPartnerPayoutConnector? connector = null,
        Guid? userId = null, IPermissionService? permissions = null)
        => new(
            context,
            partyService,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId ?? CallerUserId),
            new SinglePayoutConnectorResolver(connector ?? new RecordingPayoutConnector()),
            new Spec042NoopConnectorFactory(),
            new TestClock(),
            permissions ?? Moq.Mock.Of<IPermissionService>());

    private static void SeedCaller(FinanceDbContext context, Guid tenantId, Guid partyId, Guid? userId = null)
        => context.UserParties.Add(new UserPartyReadModel
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId ?? CallerUserId,
            PartyId = partyId
        });

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
        result.AccountName.Should().Be("Jane Doe");

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
    public async Task VerifyAndRegisterBeneficiaryAsync_Should_RegisterAndPersistVerifiedMaskedDestination()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var party = new FakePartyService();
        var customerId = party.SeedParty("John Customer");
        SeedCaller(context, tenantId, customerId);
        await context.SaveChangesAsync();
        var connector = new RecordingPayoutConnector
        {
            ResolvedName = "Alex James",
            ProviderBeneficiaryId = "rcb_abc123"
        };
        var service = CreateService(context, tenantId, party, connector);

        var request = new VerifyPayoutBeneficiaryRequest(
            CustomerPartyId: customerId,
            DestinationType: "Bank",
            Currency: "ngn",
            Country: "ng",
            AccountName: "Customer Supplied",
            AccountNumber: "0690000040",
            BankCode: "044");

        var result = await service.VerifyAndRegisterBeneficiaryAsync(request);

        result.IsVerified.Should().BeTrue();
        result.AccountName.Should().Be("Alex James");
        result.ProviderCode.Should().Be("TestPay");
        result.MaskedAccountIdentifier.Should().Be("****0040");
        connector.ResolveCalls.Should().Be(1);
        connector.RegisterCalls.Should().Be(1);

        var account = await context.ExternalPayoutAccounts.SingleAsync();
        account.IsVerified.Should().BeTrue();
        account.VerifiedAt.Should().Be(new TestClock().UtcNow);
        account.ProviderCode.Should().Be("TestPay");
        account.ProviderBeneficiaryId.Should().Be("rcb_abc123");
        account.RailFingerprint.Should().NotBeNullOrWhiteSpace();
        account.MaskedAccountIdentifier.Should().Be("****0040");
        account.MaskedAccountIdentifier.Should().NotContain("0690000040");
    }

    [Fact]
    public async Task VerifyAndRegisterBeneficiaryAsync_Should_FallbackToSuppliedName_When_NameEnquiryFails()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var party = new FakePartyService();
        var customerId = party.SeedParty("John Customer");
        SeedCaller(context, tenantId, customerId);
        await context.SaveChangesAsync();
        var connector = new RecordingPayoutConnector { ResolveThrows = true };
        var service = CreateService(context, tenantId, party, connector);

        var result = await service.VerifyAndRegisterBeneficiaryAsync(new VerifyPayoutBeneficiaryRequest(
            customerId, "Bank", "NGN", "NG", "Fallback Name", AccountNumber: "0690000040", BankCode: "044"));

        result.IsVerified.Should().BeTrue();
        result.AccountName.Should().Be("Fallback Name");
        connector.RegisteredAccountName.Should().Be("Fallback Name");
    }

    [Fact]
    public async Task VerifyAndRegisterBeneficiaryAsync_Should_NotDuplicate_When_Reverified()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var party = new FakePartyService();
        var customerId = party.SeedParty("John Customer");
        SeedCaller(context, tenantId, customerId);
        await context.SaveChangesAsync();
        var connector = new RecordingPayoutConnector { ProviderBeneficiaryId = "rcb_same" };
        var service = CreateService(context, tenantId, party, connector);
        var request = new VerifyPayoutBeneficiaryRequest(
            customerId, "Bank", "NGN", "NG", "Jane Doe", AccountNumber: "0690000040", BankCode: "044");

        var first = await service.VerifyAndRegisterBeneficiaryAsync(request);
        var second = await service.VerifyAndRegisterBeneficiaryAsync(request with { AccountName = "Changed Name" });

        second.ExternalPayoutAccountId.Should().Be(first.ExternalPayoutAccountId);
        (await context.ExternalPayoutAccounts.CountAsync()).Should().Be(1);
        connector.RegisterCalls.Should().Be(1);
    }

    [Fact]
    public async Task VerifyAndRegisterBeneficiaryAsync_Should_RejectUnauthorizedCaller_BeforeConnectorCall()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var party = new FakePartyService();
        var customerId = party.SeedParty("John Customer");
        SeedCaller(context, tenantId, Guid.NewGuid());
        await context.SaveChangesAsync();
        var connector = new RecordingPayoutConnector();
        var service = CreateService(context, tenantId, party, connector);

        var act = async () => await service.VerifyAndRegisterBeneficiaryAsync(new VerifyPayoutBeneficiaryRequest(
            customerId, "Bank", "NGN", "NG", "Jane Doe", AccountNumber: "0690000040", BankCode: "044"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        connector.ResolveCalls.Should().Be(0);
        connector.RegisterCalls.Should().Be(0);
        (await context.ExternalPayoutAccounts.CountAsync()).Should().Be(0);
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

    private static IPermissionService OperatorPermissions(params string[] permissions)
    {
        var mock = new Moq.Mock<IPermissionService>();
        mock.Setup(service => service.HasPermissionAsync(Moq.It.IsAny<Guid>(), Moq.It.IsAny<string>(), Moq.It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string permission, CancellationToken _) => permissions.Contains(permission));
        return mock.Object;
    }

    private static Guid SeedOperatorCustomer(FinanceDbContext db, Guid tenantId, FakePartyService parties)
    {
        var id = parties.SeedParty("Sending customer");
        db.Parties.Add(new PartyReadModel { Id = id, TenantId = tenantId, DisplayName = "Sending customer" });
        return id;
    }

    [Theory]
    [InlineData("Bank")]
    [InlineData("MobileMoney")]
    [InlineData("Wallet")]
    public async Task OperatorRegistration_Should_UseSharedVerificationWithoutImpersonation_AndReturnOnlyMaskedRails(string rail)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var parties = new FakePartyService();
        var customerId = SeedOperatorCustomer(db, tenantId, parties);
        await db.SaveChangesAsync();
        var connector = new RecordingPayoutConnector();
        var service = CreateService(db, tenantId, parties, connector, permissions: OperatorPermissions("Payment.Create", "Customers.Write", "Payment.Read", "Customers.Read"));
        var request = new VerifyPayoutBeneficiaryRequest(customerId, rail, "NGN", "NG", "Recipient", AccountNumber: "0690000040", BankCode: "044", MobileNetwork: "MTN", Msisdn: "+2348000000040", WalletId: "wallet-0000040");

        // Act
        var first = await service.RegisterForCustomerAsync(request);
        var replay = await service.RegisterForCustomerAsync(request);
        var list = await service.ListForCustomerAsync(customerId);

        // Assert
        first.IsVerified.Should().BeTrue();
        first.MaskedAccountIdentifier.Should().Be("****0040");
        replay.ExternalPayoutAccountId.Should().Be(first.ExternalPayoutAccountId);
        connector.RegisterCalls.Should().Be(1);
        db.UserParties.Should().BeEmpty();
        list.Beneficiaries.Should().ContainSingle().Which.Destination.Should().BeEquivalentTo(first);
        var serialized = System.Text.Json.JsonSerializer.Serialize(list);
        serialized.Should().NotContain("0690000040").And.NotContain("+2348000000040").And.NotContain("wallet-0000040").And.NotContain("rcb_test");
        await service.Invoking(service => service.VerifyAndRegisterBeneficiaryAsync(request)).Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData("Payment.Create")]
    [InlineData("Customers.Write")]
    public async Task OperatorRegistration_Should_DenyMissingPermission_BeforePartnerCalls(string permission)
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var parties = new FakePartyService();
        var customer = SeedOperatorCustomer(db, tenantId, parties);
        await db.SaveChangesAsync();
        var connector = new RecordingPayoutConnector();
        var service = CreateService(db, tenantId, parties, connector, permissions: OperatorPermissions(permission));
        var request = new VerifyPayoutBeneficiaryRequest(customer, "Bank", "NGN", "NG", "Name", AccountNumber: "0690000040", BankCode: "044");

        await service.Invoking(service => service.RegisterForCustomerAsync(request)).Should().ThrowAsync<PermissionDeniedException>();
        await service.Invoking(service => service.RemoveDestinationAsync(customer, Guid.NewGuid())).Should().ThrowAsync<PermissionDeniedException>();
        await service.Invoking(service => service.UpdateNotesAsync(customer, Guid.NewGuid(), "notes")).Should().ThrowAsync<PermissionDeniedException>();
        connector.RegisterCalls.Should().Be(0);
        db.ExternalPayoutAccounts.Should().BeEmpty();
    }

    [Fact]
    public async Task OperatorManagement_Should_DenyForeignCustomersAndRecipientParties_BeforePartnerCalls()
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var parties = new FakePartyService();
        var customer = SeedOperatorCustomer(db, tenantId, parties);
        var foreign = parties.SeedParty("Foreign party");
        db.Parties.Add(new PartyReadModel { Id = foreign, TenantId = Guid.NewGuid(), DisplayName = "Foreign party" });
        await db.SaveChangesAsync();
        var connector = new RecordingPayoutConnector();
        var service = CreateService(db, tenantId, parties, connector, permissions: OperatorPermissions("Payment.Create", "Customers.Write", "Payment.Read", "Customers.Read"));
        var request = new VerifyPayoutBeneficiaryRequest(customer, "Bank", "NGN", "NG", "Name", AccountNumber: "0690000040", BankCode: "044", BeneficiaryPartyId: foreign);

        await service.Invoking(service => service.RegisterForCustomerAsync(request)).Should().ThrowAsync<NotFoundException>();
        await service.Invoking(service => service.RegisterForCustomerAsync(request with { CustomerPartyId = foreign, BeneficiaryPartyId = null })).Should().ThrowAsync<NotFoundException>();
        await service.Invoking(service => service.ListForCustomerAsync(foreign)).Should().ThrowAsync<NotFoundException>();
        await service.Invoking(service => service.RemoveDestinationAsync(foreign, Guid.NewGuid())).Should().ThrowAsync<NotFoundException>();
        connector.RegisterCalls.Should().Be(0);
    }

    [Fact]
    public async Task OperatorManagement_Should_EditOnlyCustomerScopedNotes_AndSoftRemoveOnlyOneDestination()
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var parties = new FakePartyService();
        var customer = SeedOperatorCustomer(db, tenantId, parties);
        var other = SeedOperatorCustomer(db, tenantId, parties);
        var recipient = parties.SeedParty("Shared recipient");
        var service = CreateService(db, tenantId, parties, permissions: OperatorPermissions("Payment.Create", "Customers.Write", "Payment.Read", "Customers.Read"));
        var request = new SavePayoutBeneficiaryRequest(customer, "Bank", "Name", "NGN", "****0040", BeneficiaryPartyId: recipient);
        var first = await service.SaveBeneficiaryAsync(request);
        var second = await service.SaveBeneficiaryAsync(request with { MaskedAccountIdentifier = "****1122" });
        var otherCustomer = await service.SaveBeneficiaryAsync(request with { CustomerPartyId = other });
        await db.SaveChangesAsync();

        await service.UpdateNotesAsync(customer, recipient, "  Monthly family support  ");
        (await service.ListForCustomerAsync(customer)).Beneficiaries.Should().OnlyContain(item => item.Notes == "Monthly family support");
        (await service.ListForCustomerAsync(other)).Beneficiaries.Should().OnlyContain(item => item.Notes == null);
        await service.UpdateNotesAsync(customer, recipient, "");
        (await service.ListForCustomerAsync(customer)).Beneficiaries.Should().OnlyContain(item => item.Notes == null);
        await service.Invoking(service => service.UpdateNotesAsync(customer, recipient, new string('x', 1025))).Should().ThrowAsync<ArgumentException>();
        await service.RemoveDestinationAsync(other, first.ExternalPayoutAccountId);
        (await db.ExternalPayoutAccounts.CountAsync()).Should().Be(3);
        await service.RemoveDestinationAsync(customer, first.ExternalPayoutAccountId);
        await service.RemoveDestinationAsync(customer, first.ExternalPayoutAccountId);

        (await service.ListForCustomerAsync(customer)).Beneficiaries.Should().ContainSingle().Which.Destination.ExternalPayoutAccountId.Should().Be(second.ExternalPayoutAccountId);
        (await service.ListForCustomerAsync(other)).Beneficiaries.Should().ContainSingle().Which.Destination.ExternalPayoutAccountId.Should().Be(otherCustomer.ExternalPayoutAccountId);
        var deleted = await db.ExternalPayoutAccounts.IgnoreQueryFilters().SingleAsync(account => account.Id == first.ExternalPayoutAccountId);
        deleted.IsDeleted.Should().BeTrue();
        deleted.DeletedBy.Should().Be(CallerUserId);
        deleted.DeletedAt.Should().NotBeNull();
        parties.Relationships.Should().HaveCount(2);
    }

    /// <summary>
    /// Stateful in-memory <see cref="IPartyService"/>: stores parties, relationship edges, and role
    /// assignments so tests can assert what the service wrote across the seam. Mirrors the real
    /// implementation's idempotent role assignment; relationship inserts are recorded verbatim so the
    /// service's own find-or-create dedupe is what's under test.
    /// </summary>
    private sealed class FakePartyService : IPartyService
    {
        private readonly Dictionary<(Guid, Guid, string), Guid> _edgeIds = new();
        private readonly Dictionary<Guid, string?> _notes = new();
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

        public Task<PartyResponse> EnsureUnverifiedGuestPartyAsync(Guid partyId, Guid checkoutId,
            CreatePartyRequest details, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PartyResponse?> GetPartyAsync(Guid partyId, CancellationToken cancellationToken = default)
            => Task.FromResult(_parties.TryGetValue(partyId, out var party) ? party : null);

        public Task<RelatedPartyResponse> CreateRelatedPartyAsync(CreateRelatedPartyRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PartyRelationshipResponse> CreateRelationshipAsync(CreatePartyRelationshipRequest request, CancellationToken cancellationToken = default)
        {
            Relationships.Add((request.FromPartyId, request.ToPartyId, request.RelationshipTypeCode));
            var edge = BuildRelationship(request.FromPartyId, request.ToPartyId, request.RelationshipTypeCode);
            _notes[edge.RelationshipId] = request.Notes;
            return Task.FromResult(edge with { Notes = request.Notes });
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

        public Task<IReadOnlyList<PartyPhotoUrls>> GetPartyPhotosAsync(IReadOnlyCollection<Guid> partyIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PartyPhotoUrls>>(Array.Empty<PartyPhotoUrls>());

        public Task<PartyPhotoUrls> SetPartyPhotoAsync(Guid partyId, string contentType, Stream photo, CancellationToken cancellationToken = default)
            => Task.FromResult(new PartyPhotoUrls(partyId, null, null, null, null));

        public Task<bool> UpdateRelationshipAsync(Guid relationshipId, string? relationshipTypeCode = null, string? notes = null, bool? isActive = null, CancellationToken cancellationToken = default)
        {
            if (!_edgeIds.ContainsValue(relationshipId)) return Task.FromResult(false);
            if (notes is not null) _notes[relationshipId] = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            return Task.FromResult(true);
        }

        private PartyRelationshipResponse BuildRelationship(Guid fromPartyId, Guid toPartyId, string typeCode)
        {
            var key = (fromPartyId, toPartyId, typeCode);
            if (!_edgeIds.TryGetValue(key, out var id)) _edgeIds[key] = id = Guid.NewGuid();
            return new(
                id,
                fromPartyId,
                _parties.TryGetValue(fromPartyId, out var from) ? from.DisplayName : string.Empty,
                toPartyId,
                _parties.TryGetValue(toPartyId, out var to) ? to.DisplayName : string.Empty,
                typeCode,
                typeCode,
                true,
                _notes.GetValueOrDefault(id));
        }
    }

    private sealed class RecordingPayoutConnector : IPartnerPayoutConnector
    {
        public string ProviderCode => "TestPay";
        public string? ResolvedName { get; init; } = "TEST ACCOUNT HOLDER";
        public string ProviderBeneficiaryId { get; init; } = "rcb_test";
        public bool ResolveThrows { get; init; }
        public int ResolveCalls { get; private set; }
        public int RegisterCalls { get; private set; }
        public string? RegisteredAccountName { get; private set; }

        public IReadOnlyCollection<PartnerConnectorCapability> Capabilities { get; } = new[]
        {
            new PartnerConnectorCapability(
                PartnerServiceCategory.Payout, new[] { "NG" }, new[] { "NGN" }, new[] { "Bank", "MobileMoney", "Wallet" })
        };

        public Task<PayoutInitiationResult> InitiatePayoutAsync(PayoutInstruction instruction, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PayoutStatusResult> GetPayoutStatusAsync(PartnerReference reference, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PayoutQuoteResult> QuotePayoutAsync(PayoutQuoteRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AccountResolutionResult> ResolveAccountAsync(AccountResolutionRequest request, CancellationToken cancellationToken = default)
        {
            ResolveCalls++;
            if (ResolveThrows)
            {
                throw new InvalidOperationException("resolve unavailable");
            }

            return Task.FromResult(new AccountResolutionResult(
                true, ResolvedName, new RawProviderResponse("00", "resolved", null)));
        }

        public Task<RecipientRegistrationResult> RegisterRecipientAsync(
            RecipientRegistrationRequest request, CancellationToken cancellationToken = default)
        {
            RegisterCalls++;
            RegisteredAccountName = request.AccountName;
            return Task.FromResult(new RecipientRegistrationResult(
                true, ProviderBeneficiaryId, request.AccountName, new RawProviderResponse("00", "registered", null)));
        }
    }

    private sealed class SinglePayoutConnectorResolver(IPartnerPayoutConnector connector) : IPartnerConnectorResolver
    {
        public IPartnerPayoutConnector ResolvePayoutConnector(string providerCode)
            => string.Equals(providerCode, connector.ProviderCode, StringComparison.OrdinalIgnoreCase)
                ? connector
                : throw new InvalidOperationException("Connector not configured.");

        public IPartnerCollectionConnector ResolveCollectionConnector(string providerCode) => throw new NotSupportedException();
        public IPartnerBillPaymentConnector ResolveBillPaymentConnector(string providerCode) => throw new NotSupportedException();
        public IPartnerWebhookTranslator ResolveWebhookTranslator(string providerCode) => throw new NotSupportedException();

        public bool TryResolvePayoutConnector(PartnerConnectorQuery query, out IPartnerPayoutConnector? resolved)
        {
            resolved = connector;
            return true;
        }

        public bool TryResolvePreferredPayoutConnector(PartnerConnectorQuery query, out IPartnerPayoutConnector? resolved)
        {
            resolved = connector;
            return true;
        }

        public bool TryResolveCollectionConnector(PartnerConnectorQuery query, out IPartnerCollectionConnector? resolved)
        {
            resolved = null;
            return false;
        }

        public bool TryResolveBillPaymentConnector(PartnerConnectorQuery query, out IPartnerBillPaymentConnector? resolved)
        {
            resolved = null;
            return false;
        }
    }
}
