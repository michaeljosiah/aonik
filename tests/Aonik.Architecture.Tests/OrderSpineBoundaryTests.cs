using System.Reflection;

using FluentAssertions;

using NetArchTest.Rules;

namespace Aonik.Architecture.Tests;

/// <summary>
/// Build-time guard rails for the unified Order spine (Spec 041 / ADR-011, requirement R6
/// and the §22 verification "Boundary" check).
///
/// Two invariants are asserted:
/// <list type="number">
///   <item>
///     <b>No sibling coupling</b> — <c>Aonik.Finance</c> and <c>Aonik.Commerce</c> never reference
///     each other in either direction. Both depend <em>downward</em> on the shared
///     <c>Aonik.Ordering</c> middle layer / <c>SharedKernel.Abstractions.Ordering</c> contract; a
///     sibling-to-sibling reference is forbidden by ADR-005.
///   </item>
///   <item>
///     <b>One sanctioned entity home</b> — the anemic Order entity types live only in the
///     <c>Aonik.Ordering</c> assembly (the §25 placement decision; namespaces are deliberately
///     preserved as <c>Aonik.Finance.Entities.Orders</c> so the relocation needed no migration).
///     They must not be re-introduced into <c>Aonik.Finance</c> (or any other module assembly).
///   </item>
/// </list>
/// </summary>
/// <remarks>
/// The reference checks read each assembly's compiled <see cref="Assembly.GetReferencedAssemblies"/>
/// metadata, which lists a sibling assembly whenever a (consumed) ProjectReference exists. The
/// entity-home checks reflect over the types actually <em>defined in</em> each assembly
/// (<see cref="Assembly.GetTypes"/> never returns types merely referenced from another assembly),
/// so "lives in Aonik.Ordering, not Aonik.Finance" is asserted directly against where the type
/// is compiled — independent of its preserved C# namespace.
/// </remarks>
public class OrderSpineBoundaryTests
{
    private const string Finance = "Aonik.Finance";
    private const string Commerce = "Aonik.Commerce";
    private const string Ordering = "Aonik.Ordering";

    /// <summary>The preserved namespace the relocated Order entities still declare.</summary>
    private const string OrderEntityNamespace = "Aonik.Finance.Entities.Orders";

    /// <summary>
    /// The persisted, anemic Order entity types whose single sanctioned home is
    /// <c>Aonik.Ordering</c>. Constants/enums (OrderType, OrderStatuses, …) are intentionally
    /// excluded — the assertion targets the EF-mapped aggregate, which is the bit that must not be
    /// duplicated across module assemblies.
    /// </summary>
    private static readonly string[] OrderEntityTypeNames =
    [
        "Order",
        "OrderItem",
        "OrderPartyRole",
        "OrderFundingRef",
        "OrderFulfilmentRef",
        "OrderHistoryEvent",
        "OrderNote",
    ];

    [Fact]
    public void Finance_Should_NotReference_Commerce()
    {
        // R6: a remittance/bill-pay path must never reach into the retail domain. Both build on the
        // shared Ordering layer instead.
        var finance = Assembly.Load(Finance);

        var referencesCommerce = finance.GetReferencedAssemblies()
            .Any(a => string.Equals(a.Name, Commerce, StringComparison.Ordinal));

        referencesCommerce.Should().BeFalse(
            "Aonik.Finance must not hold a ProjectReference to Aonik.Commerce (ADR-005 / Spec 041 R6) — "
            + "the Order spine is shared via Aonik.Ordering, not via a sibling reference.");
    }

    [Fact]
    public void Commerce_Should_NotReference_Finance()
    {
        // R6 (the other direction): the retail domain reuses Invoice/PaymentIntent through
        // SharedKernel write contracts, never by referencing Aonik.Finance directly.
        var commerce = Assembly.Load(Commerce);

        var referencesFinance = commerce.GetReferencedAssemblies()
            .Any(a => string.Equals(a.Name, Finance, StringComparison.Ordinal));

        referencesFinance.Should().BeFalse(
            "Aonik.Commerce must not hold a ProjectReference to Aonik.Finance (ADR-005 / Spec 041 R6) — "
            + "it depends downward on Aonik.Ordering + SharedKernel only.");
    }

    [Fact]
    public void OrderEntities_Should_LiveIn_OrderingAssembly()
    {
        // The §25 placement decision: the anemic Order aggregate is owned by Aonik.Ordering. Assert
        // every expected entity type is actually defined there, so an accidental relocation/removal
        // is caught.
        var ordering = Assembly.Load(Ordering);

        var definedHere = ordering.GetTypes()
            .Where(t => string.Equals(t.Namespace, OrderEntityNamespace, StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        definedHere.Should().Contain(
            OrderEntityTypeNames,
            "the Order entity aggregate is owned by the Aonik.Ordering module (Spec 041 / ADR-011).");
    }

    [Fact]
    public void OrderEntities_Should_NotLiveIn_FinanceAssembly()
    {
        // The mirror check: Finance keeps only type-specific orchestration and holds NO Order
        // entity (R5). The types it consumes come from the Aonik.Ordering assembly via a downward
        // ProjectReference, so GetTypes() on the Finance assembly must not define any of them.
        var finance = Assembly.Load(Finance);

        var orderEntitiesDefinedInFinance = finance.GetTypes()
            .Where(t => string.Equals(t.Namespace, OrderEntityNamespace, StringComparison.Ordinal)
                && OrderEntityTypeNames.Contains(t.Name, StringComparer.Ordinal))
            .Select(t => t.FullName)
            .ToList();

        orderEntitiesDefinedInFinance.Should().BeEmpty(
            "Aonik.Finance must hold no Order entity (Spec 041 R5); the relocated aggregate lives in "
            + "Aonik.Ordering and is consumed via a downward project reference.");
    }

    [Fact]
    public void OrderEntities_Should_NotBeDefinedIn_AnyOtherModuleAssembly()
    {
        // Belt-and-braces: confirm the aggregate has exactly one home by checking the other
        // domain/runtime module assemblies do not define it either. NetArchTest's reflection
        // honours "defined in this assembly" semantics, matching the intent of a single owner.
        string[] otherModules = ["Aonik.Platform", "Aonik.Ai", "Aonik.Agents", Commerce, "Aonik.SharedKernel"];

        foreach (var moduleName in otherModules)
        {
            var assembly = Assembly.Load(moduleName);

            var offenders = assembly.GetTypes()
                .Where(t => string.Equals(t.Namespace, OrderEntityNamespace, StringComparison.Ordinal)
                    && OrderEntityTypeNames.Contains(t.Name, StringComparer.Ordinal))
                .Select(t => t.FullName)
                .ToList();

            offenders.Should().BeEmpty(
                $"{moduleName} must not define any Order entity type — the sanctioned home is Aonik.Ordering.");
        }
    }
}
