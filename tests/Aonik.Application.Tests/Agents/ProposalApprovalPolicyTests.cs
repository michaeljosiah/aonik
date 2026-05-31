using Aonik.Agents.Services;
using Aonik.SharedKernel.Abstractions.Agents;

using FluentAssertions;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Spec 032 §8.2 — the <see cref="IProposalApprovalPolicy"/> seam that replaced the coarse
/// <c>AdminUserPolicy</c> role gate on the proposal approve / dismiss endpoints. v1 enforces the
/// two product-agnostic invariants: an authenticated user must be deciding, and the actor's tenant
/// must match the proposal's tenant. (Self-approval / SoD / step-up are deferred until the proposal
/// persists an originating-user id.)
/// </summary>
public class ProposalApprovalPolicyTests
{
    private static ProposalAuthorizationContext Context(Guid tenantId) =>
        new(
            ProposalId: Guid.NewGuid(),
            TenantId: tenantId,
            ProposalType: "Finance.CapturePayment",
            RiskTier: "High",
            OriginatingUserId: null);

    [Fact]
    public void Authorize_Should_Allow_When_AuthenticatedUserDecidesOwnTenant()
    {
        var policy = new ProposalApprovalPolicy();
        var tenantId = Guid.NewGuid();
        var actor = new ApprovalActor(UserId: Guid.NewGuid(), TenantId: tenantId);

        var result = policy.Authorize(actor, Context(tenantId));

        result.IsAuthorized.Should().BeTrue();
        result.Reason.Should().BeNull();
    }

    [Fact]
    public void Authorize_Should_Deny_When_NoAuthenticatedUser()
    {
        var policy = new ProposalApprovalPolicy();
        var tenantId = Guid.NewGuid();
        var actor = new ApprovalActor(UserId: null, TenantId: tenantId);

        var result = policy.Authorize(actor, Context(tenantId));

        result.IsAuthorized.Should().BeFalse();
        result.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Authorize_Should_Deny_When_ActorTenantDiffersFromProposalTenant()
    {
        var policy = new ProposalApprovalPolicy();
        var actor = new ApprovalActor(UserId: Guid.NewGuid(), TenantId: Guid.NewGuid());

        // Different tenant on the proposal than the deciding actor.
        var result = policy.Authorize(actor, Context(Guid.NewGuid()));

        result.IsAuthorized.Should().BeFalse();
        result.Reason.Should().Contain("tenant");
    }

    [Fact]
    public void Authorize_Should_Throw_When_ActorOrContextNull()
    {
        var policy = new ProposalApprovalPolicy();
        var context = Context(Guid.NewGuid());

        FluentActions.Invoking(() => policy.Authorize(null!, context))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => policy.Authorize(new ApprovalActor(Guid.NewGuid(), Guid.NewGuid()), null!))
            .Should().Throw<ArgumentNullException>();
    }
}
