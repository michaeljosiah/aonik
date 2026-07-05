using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Services;
using FluentAssertions;

namespace Aonik.Application.Tests.PersonalFinance;

public class FinancialLifeGraphSchemaServiceTests
{
    private static FinancialLifeGraphSchemaService CreateService()
    {
        var schema = new FinancialLifeGraphSchema();
        return new FinancialLifeGraphSchemaService(schema);
    }

    [Fact]
    public void GetFullSchema_ShouldReturnAllNodeTypesAndPredicates()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetFullSchema();

        // Assert
        result.Should().NotBeNull();
        result.NodeTypes.Should().NotBeEmpty();
        result.Predicates.Should().NotBeEmpty();
        result.TotalEdgeRules.Should().BeGreaterThan(0);

        // Verify known node types are present
        result.NodeTypes.Select(nt => nt.NodeType).Should().Contain(FinancialLifeGraphNodeTypes.PersonalAccount);
        result.NodeTypes.Select(nt => nt.NodeType).Should().Contain(FinancialLifeGraphNodeTypes.Bill);
        result.NodeTypes.Select(nt => nt.NodeType).Should().Contain(FinancialLifeGraphNodeTypes.Goal);
        result.NodeTypes.Select(nt => nt.NodeType).Should().Contain(FinancialLifeGraphNodeTypes.Subscription);
        result.NodeTypes.Select(nt => nt.NodeType).Should().Contain(FinancialLifeGraphNodeTypes.PersonalTransaction);
        result.NodeTypes.Select(nt => nt.NodeType).Should().Contain(FinancialLifeGraphNodeTypes.UserRoot);

        // Verify known predicates are present
        result.Predicates.Select(p => p.Predicate).Should().Contain(FinancialLifeGraphPredicates.OwnsAccount);
        result.Predicates.Select(p => p.Predicate).Should().Contain(FinancialLifeGraphPredicates.HasTransaction);
        result.Predicates.Select(p => p.Predicate).Should().Contain(FinancialLifeGraphPredicates.HasBill);
    }

    [Fact]
    public void GetFullSchema_NodeTypes_ShouldHaveDescriptions()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetFullSchema();

        // Assert
        foreach (var nodeType in result.NodeTypes)
        {
            nodeType.Description.Should().NotBeNullOrWhiteSpace(
                $"NodeType '{nodeType.NodeType}' should have a description");
        }
    }

    [Fact]
    public void GetFullSchema_NodeTypes_ShouldHaveOutboundAndInboundEdges()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetFullSchema();

        // Assert — UserRoot should have outbound edges (OWNS_ACCOUNT, HAS_BILL, etc.)
        var userRoot = result.NodeTypes.First(nt => nt.NodeType == FinancialLifeGraphNodeTypes.UserRoot);
        userRoot.OutboundEdges.Should().NotBeEmpty("UserRoot should have outbound edges");

        // PersonalAccount should have inbound edges (from OWNS_ACCOUNT)
        var personalAccount = result.NodeTypes.First(nt => nt.NodeType == FinancialLifeGraphNodeTypes.PersonalAccount);
        personalAccount.InboundEdges.Should().NotBeEmpty("PersonalAccount should have inbound edges");
    }

    [Fact]
    public void GetFullSchema_Predicates_ShouldHaveAllowedConnections()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetFullSchema();

        // Assert
        foreach (var predicate in result.Predicates)
        {
            predicate.AllowedConnections.Should().NotBeEmpty(
                $"Predicate '{predicate.Predicate}' should have allowed connections");
        }
    }

    [Fact]
    public void GetNodeTypeSchema_ShouldReturnDetails_WhenNodeTypeExists()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetNodeTypeSchema(FinancialLifeGraphNodeTypes.PersonalAccount);

        // Assert
        result.Should().NotBeNull();
        result!.NodeType.Should().Be(FinancialLifeGraphNodeTypes.PersonalAccount);
        result.Description.Should().NotBeNullOrWhiteSpace();
        result.IsMirrorProjection.Should().BeTrue("PersonalAccount is a mirror projection of the underlying entity");
    }

    [Fact]
    public void GetNodeTypeSchema_ShouldReturnNull_WhenNodeTypeDoesNotExist()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetNodeTypeSchema("NonExistentNodeType");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetNodeTypeSchema_ShouldIncludeEdges_ForKnownNodeType()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetNodeTypeSchema(FinancialLifeGraphNodeTypes.Bill);

        // Assert
        result.Should().NotBeNull();
        // Bill should have inbound edges (HAS_BILL from UserRoot)
        result!.InboundEdges.Should().Contain(e =>
            e.Predicate == FinancialLifeGraphPredicates.HasBill
            && e.FromNodeType == FinancialLifeGraphNodeTypes.UserRoot);
    }

    [Fact]
    public void GetNodeTypeSchema_ShouldIdentifyMirrorProjections()
    {
        // Arrange
        var service = CreateService();

        // Act — PersonalTransaction is a mirror projection
        var result = service.GetNodeTypeSchema(FinancialLifeGraphNodeTypes.PersonalTransaction);

        // Assert
        result.Should().NotBeNull();
        result!.IsMirrorProjection.Should().BeTrue("PersonalTransaction is a mirror projection");
        result.CanBeCreatedNatively.Should().BeFalse("Mirror projections cannot be created natively");
    }

    [Fact]
    public void GetCompactSchemaPrompt_ShouldReturnNonEmptyString()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetCompactSchemaPrompt();

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain("Node Types");
    }

    [Fact]
    public void GetFullSchema_Predicates_ShouldHaveReasoningHints()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetFullSchema();

        // Assert
        foreach (var predicate in result.Predicates)
        {
            predicate.ReasoningHint.Should().NotBeNullOrWhiteSpace(
                $"Predicate '{predicate.Predicate}' should have a reasoning hint");
        }
    }

    [Fact]
    public void GetFullSchema_ShouldBeConsistentWithTotalEdgeRules()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetFullSchema();

        // Assert — TotalEdgeRules should equal sum of all allowed connections across predicates
        var totalConnections = result.Predicates.Sum(p => p.AllowedConnections.Count);
        result.TotalEdgeRules.Should().Be(totalConnections);
    }

    [Fact]
    public void GetFullSchema_NodeTypes_ShouldBeSortedAlphabetically()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetFullSchema();

        // Assert
        var nodeTypeNames = result.NodeTypes.Select(nt => nt.NodeType).ToList();
        nodeTypeNames.Should().BeInAscendingOrder();
    }
}
