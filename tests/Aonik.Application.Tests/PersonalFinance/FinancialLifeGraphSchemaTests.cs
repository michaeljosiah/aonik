using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.PersonalFinance;

public class FinancialLifeGraphSchemaTests
{
    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId)
        {
            _tenantId = tenantId;
        }

        public Guid GetCurrentTenantId() => _tenantId;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid _userId;

        public TestCurrentUserProvider(Guid userId)
        {
            _userId = userId;
        }

        public Guid? GetCurrentUserId() => _userId;

        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = _userId;
            return true;
        }
    }

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"FinancialLifeGraphSchema_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    [Fact]
    public void Schema_Should_Define_Spec_NodeTypes_And_Predicates()
    {
        // Arrange
        var schema = new FinancialLifeGraphSchema();

        // Assert
        schema.NodeTypes.Keys.Should().Contain(new[]
        {
            "UserRoot",
            "Household",
            "Party",
            "PersonalAccount",
            "PersonalLinkedAccount",
            "PersonalTransaction",
            "Bill",
            "Goal",
            "Subscription",
            "FxQuote",
            "OrderRef",
            "PaymentIntentRef",
            "NativeAnnotation"
        });

        schema.Predicates.Should().Contain(new[]
        {
            "OWNS_ACCOUNT",
            "HAS_TRANSACTION",
            "HAS_BILL",
            "HAS_GOAL",
            "HAS_SUBSCRIPTION",
            "BELONGS_TO_HOUSEHOLD",
            "HOUSEHOLD_HAS_MEMBER",
            "RELATED_TO_PARTY",
            "USES_ACCOUNT",
            "USES_LINKED_ACCOUNT",
            "LINKED_TO_ORDER",
            "LINKED_TO_INVOICE",
            "LINKED_TO_PAYMENT_INTENT",
            "FUNDED_BY_ACCOUNT",
            "ANNOTATED_AS"
        });
    }

    [Fact]
    public void Schema_Should_Enforce_Edge_Matrix()
    {
        // Arrange
        var schema = new FinancialLifeGraphSchema();

        // Assert
        schema.IsAllowedEdge("Goal", "FUNDED_BY_ACCOUNT", "PersonalAccount", requireNativeCreatable: true).Should().BeTrue();
        schema.IsAllowedEdge("UserRoot", "RELATED_TO_PARTY", "Party", requireNativeCreatable: true).Should().BeTrue();
        schema.IsAllowedEdge("Party", "FUNDED_BY_ACCOUNT", "PersonalAccount", requireNativeCreatable: true).Should().BeFalse();
        schema.IsAllowedEdge("UserRoot", "OWNS_ACCOUNT", "PersonalAccount", requireNativeCreatable: true).Should().BeFalse();
        schema.IsAllowedEdge("UserRoot", "OWNS_ACCOUNT", "PersonalAccount", requireNativeCreatable: false).Should().BeTrue();
    }

    [Fact]
    public async Task ValidateNodeCreateAsync_Should_Reject_Mirror_Node_Types()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var context = CreateDbContext(tenantId);
        var service = new FinancialLifeGraphValidationService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new FinancialLifeGraphSchema());

        // Act
        Func<Task> action = () => service.ValidateNodeCreateAsync(new CreateFinancialLifeGraphNodeRequest(
            "Goal",
            "Mirror goal",
            "{}",
            null,
            null,
            null,
            FinancialLifeGraphEntityStatus.Active,
            false,
            null));

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reserved for mirror projection*");
    }

    [Fact]
    public async Task ValidateEdgeCreateAsync_Should_Reject_Unsupported_Matrix_Combinations()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var context = CreateDbContext(tenantId);
        var service = new FinancialLifeGraphValidationService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new FinancialLifeGraphSchema());

        var nodeTypesByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [$"user:{userId:D}"] = "UserRoot",
            ["party:11111111-1111-1111-1111-111111111111"] = "Party"
        };

        // Act
        Func<Task> action = () => service.ValidateEdgeCreateAsync(
            new CreateFinancialLifeGraphEdgeRequest(
                $"user:{userId:D}",
                "FUNDED_BY_ACCOUNT",
                "party:11111111-1111-1111-1111-111111111111",
                "{}",
                null,
                FinancialLifeGraphEntityStatus.Active,
                false,
                null),
            nodeTypesByKey);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not permitted by the Financial Life Graph schema*");
    }

    [Fact]
    public async Task ValidateEdgeCreateAsync_Should_Allow_Schema_Defined_Native_Combinations()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var context = CreateDbContext(tenantId);
        var service = new FinancialLifeGraphValidationService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new FinancialLifeGraphSchema());

        var nodeTypesByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["goal:11111111-1111-1111-1111-111111111111"] = "Goal",
            ["personal-account:22222222-2222-2222-2222-222222222222"] = "PersonalAccount"
        };

        // Act
        var action = async () => await service.ValidateEdgeCreateAsync(
            new CreateFinancialLifeGraphEdgeRequest(
                "goal:11111111-1111-1111-1111-111111111111",
                "FUNDED_BY_ACCOUNT",
                "personal-account:22222222-2222-2222-2222-222222222222",
                "{}",
                null,
                FinancialLifeGraphEntityStatus.Active,
                false,
                null),
            nodeTypesByKey);

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateEdgeCreateAsync_Should_Reject_RelatedPartyEdge_WhenCanonicalRelationshipExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var selfPartyId = Guid.NewGuid();
        var relatedPartyId = Guid.NewGuid();
        await using var context = CreateDbContext(tenantId);
        context.PersonalProfiles.Add(new Aonik.Finance.Entities.PersonalFinance.PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = selfPartyId
        });
        context.PartyRelationships.Add(new Aonik.Finance.Entities.PartyRelationshipReadModel
        {
            TenantId = tenantId,
            FromPartyId = selfPartyId,
            ToPartyId = relatedPartyId,
            RelationshipTypeCode = "Sibling",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new FinancialLifeGraphValidationService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new FinancialLifeGraphSchema());

        var nodeTypesByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [$"user:{userId:D}"] = FinancialLifeGraphNodeTypes.UserRoot,
            [$"party:{relatedPartyId:D}"] = FinancialLifeGraphNodeTypes.Party
        };

        // Act
        Func<Task> action = () => service.ValidateEdgeCreateAsync(
            new CreateFinancialLifeGraphEdgeRequest(
                $"user:{userId:D}",
                FinancialLifeGraphPredicates.RelatedToParty,
                $"party:{relatedPartyId:D}",
                "{}",
                null,
                FinancialLifeGraphEntityStatus.Active,
                false,
                null),
            nodeTypesByKey);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*canonical PartyRelationship already represents this related party link*");
    }
}
