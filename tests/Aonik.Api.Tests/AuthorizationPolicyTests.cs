using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

using Aonik.Finance.Contracts.Api.Payments;

namespace Aonik.Api.Tests;

/// <summary>
/// Regression guard for H12: the read-only operator role must never satisfy a
/// policy that authorizes a state change. Mutating finance endpoints are gated by
/// <c>AdminUserWritePolicy</c> (AdminUserPolicy minus ReadOnly); read endpoints stay
/// on <c>AdminUserPolicy</c> so ReadOnly keeps its read access. Operations and
/// PersonalUser remain in the write policy, so neither operator nor Payabo (B2C)
/// self-service is affected.
///
/// AONIK authorizes in two independent layers: (1) the endpoint <c>Policies(...)</c>
/// gate (the H12 surface), and (2) a service-level <c>EnsurePermissionAsync(...)</c>
/// check inside the service method (e.g. CreatePaymentIntentAsync requires
/// "Payment.Create"; GetPaymentIntentAsync requires "Payment.Read"). To isolate the
/// policy layer, each principal below is granted the permission its service path
/// requires — so the service check always passes and the ONLY gate that can deny is
/// the endpoint policy. The ReadOnly-write 403 is therefore unambiguously a policy
/// (role) denial, which is exactly what H12 fixes: ReadOnly holds Payment.Create
/// here yet is still blocked, purely because its role is excluded from the write policy.
/// </summary>
public class AuthorizationPolicyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthorizationPolicyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MutatingFinanceEndpoint_Should_ReturnForbidden_When_RoleIsReadOnly()
    {
        // Arrange — a ReadOnly principal that DOES hold the write permission, so the
        // service-level check passes and only the endpoint policy can deny.
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithRoles("ReadOnly").WithPermissions("Payment.Create"));

        var request = new CreatePaymentIntentRequest(100.00m, "USD", "AUTHZ-RO", Guid.NewGuid(), null);

        // Act — attempt a state-changing finance call.
        var response = await client.PostAsJsonAsync("/payments/intents", request);

        // Assert — denied purely because ReadOnly is excluded from AdminUserWritePolicy.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MutatingFinanceEndpoint_Should_Succeed_When_RoleIsOperations()
    {
        // Arrange — an Operations operator (in the write policy) with the write permission.
        var options = TestAuthOptions.Create().WithRoles("Operations").WithPermissions("Payment.Create");
        var client = await _factory.CreateAuthenticatedClientAsync(options);
        var orderId = await _factory.SeedOrderAsync(options.TenantId!.Value, payerPartyId: Guid.NewGuid());

        var request = new CreatePaymentIntentRequest(100.00m, "USD", "AUTHZ-OPS", orderId, null, PaymentMethodType: "Card");

        // Act — identical call to the ReadOnly case above; only the role differs.
        var response = await client.PostAsJsonAsync("/payments/intents", request);

        // Assert — legitimate operator writers are unaffected by the split.
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task MutatingFinanceEndpoint_Should_NotReturnForbidden_When_RoleIsPersonalUser()
    {
        // Arrange — a Payabo (B2C) self-service principal (retained in the write policy).
        var options = TestAuthOptions.Create().WithRoles("PersonalUser").WithPermissions("Payment.Create");
        var client = await _factory.CreateAuthenticatedClientAsync(options);
        var orderId = await _factory.SeedOrderAsync(options.TenantId!.Value, payerPartyId: Guid.NewGuid());

        var request = new CreatePaymentIntentRequest(100.00m, "USD", "AUTHZ-PU", orderId, null, PaymentMethodType: "Card");

        // Act
        var response = await client.PostAsJsonAsync("/payments/intents", request);

        // Assert — PersonalUser stays in the write policy, so B2C self-service is not regressed.
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReadFinanceEndpoint_Should_NotReturnForbidden_When_RoleIsReadOnly()
    {
        // Arrange — the same ReadOnly principal, now with the read permission.
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithRoles("ReadOnly").WithPermissions("Payment.Read"));

        // Act — a read endpoint that stays on AdminUserPolicy.
        var response = await client.GetAsync($"/payments/intents/{Guid.NewGuid()}");

        // Assert — ReadOnly retains read access: authorized-but-absent (404), never 403.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
