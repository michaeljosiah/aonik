using System.Net;
using System.Net.Http.Json;

using Aonik.Finance.Contracts.Api.Remittance;

using FluentAssertions;

namespace Aonik.Api.Tests;

public class OperatorRemittanceEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private static RemittanceQuoteRequest Quote() => new(Guid.NewGuid(), "GB", "NG", "GBP", "NGN", 100m, null, null, "FamilySupport", null);
    private static ConfirmOperatorRemittanceRequest Confirm(bool execution = true) => new(
        new ConfirmRemittanceRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "FamilySupport", null, null, null), execution);

    [Theory]
    [InlineData("ReadOnly", true)]
    [InlineData("PersonalUser", true)]
    [InlineData("Operations", false)]
    public async Task BouncedRecipient_Should_RequireStaffWritePolicyAndCustomerWrite(string role, bool writePermission)
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles(role)
            .WithPermissions(writePermission ? ["Payment.Read", "Customers.Read", "Customers.Write"] : ["Payment.Read", "Customers.Read"]));
        var path = $"/remittance/{Guid.NewGuid()}/receipt/email/{Guid.NewGuid()}/recipient";
        var body = new Aonik.Finance.Contracts.Services.Remittance.UpdateReceiptRecipientRequest("old@example.test", "new@example.test", true);
        (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PutAsJsonAsync(path, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BouncedRecipient_Should_ValidateConfirmationAndEmail_AndNotRevealUnknownTransfers()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("Operations")
            .WithPermissions("Payment.Read", "Customers.Read", "Customers.Write"));
        var path = $"/remittance/{Guid.NewGuid()}/receipt/email/{Guid.NewGuid()}/recipient";
        var body = new Aonik.Finance.Contracts.Services.Remittance.UpdateReceiptRecipientRequest("old@example.test", "new@example.test", false);
        (await client.PutAsJsonAsync(path, body)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.PutAsJsonAsync(path, body with { ConfirmUpdate = true, NewEmail = "invalid" })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PutAsJsonAsync(path, body with { ConfirmUpdate = true })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("PersonalUser", true, HttpStatusCode.Forbidden)]
    [InlineData("ReadOnly", false, HttpStatusCode.Forbidden)]
    [InlineData("ReadOnly", true, HttpStatusCode.NotFound)]
    public async Task ReceiptHistory_Should_AllowStaffReadAccess_AndEnforceCustomerRead(string role, bool customerRead, HttpStatusCode expected)
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles(role)
            .WithPermissions(customerRead ? ["Payment.Read", "Customers.Read"] : ["Payment.Read"]));
        (await client.GetAsync($"/remittance/{Guid.NewGuid()}/receipt/email/history")).StatusCode.Should().Be(expected);
    }

    [Theory]
    [InlineData("pageNumber=0")]
    [InlineData("pageSize=51")]
    [InlineData("pageNumber=2147483647")]
    public async Task ReceiptHistory_Should_ValidatePagination(string query)
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("ReadOnly").WithPermissions("Payment.Read", "Customers.Read"));
        (await client.GetAsync($"/remittance/{Guid.NewGuid()}/receipt/email/history?{query}")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("ReadOnly", true)]
    [InlineData("PersonalUser", true)]
    [InlineData("Operations", false)]
    public async Task ReceiptEmail_Should_DenyCallersWithoutStaffWritePolicyAndCustomerWritePermission(string role, bool writePermission)
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles(role)
            .WithPermissions(writePermission ? ["Payment.Read", "Customers.Read", "Customers.Write"] : ["Payment.Read", "Customers.Read"]));
        var path = $"/remittance/{Guid.NewGuid()}/receipt/email";
        var request = new Aonik.Finance.Contracts.Services.Remittance.SendReceiptEmailRequest(Guid.NewGuid(), Guid.NewGuid(), "customer@example.test", true);

        (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync(path, request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"{path}/{request.RequestId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReceiptEmail_Should_RequireConfirmation_AndResolveUnknownTransfersWithoutSending()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("Operations")
            .WithPermissions("Payment.Read", "Customers.Read", "Customers.Write"));
        var path = $"/remittance/{Guid.NewGuid()}/receipt";
        var request = new Aonik.Finance.Contracts.Services.Remittance.SendReceiptEmailRequest(Guid.NewGuid(), Guid.NewGuid(), "customer@example.test", false);

        (await client.PostAsJsonAsync($"{path}/email", request)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"{path}/email")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync($"{path}/email", request with { ConfirmSend = true })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("PersonalUser")]
    [InlineData("ReadOnly")]
    public async Task Quote_Should_DenyNonWritingStaffRoles_EvenWithPaymentPermission(string role)
    {
        // Arrange
        var client = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles(role).WithPermissions("Payment.Create", "Payment.Read", "Payment.Capture"));

        // Act
        var response = await client.PostAsJsonAsync("/remittance/quote", Quote());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Confirm_Should_DenyStaffWithoutCapturePermission()
    {
        // Arrange
        var client = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("Operations").WithPermissions("Payment.Create", "Payment.Read"));
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        // Act
        var response = await client.PostAsJsonAsync("/remittance/confirm", Confirm());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Confirm_Should_RejectMissingExplicitConfirmationOrIdempotencyKey_BeforeExecution()
    {
        // Arrange
        var client = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("Operations").WithPermissions("Payment.Create", "Payment.Read", "Payment.Capture"));
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");

        // Act
        var noApproval = await client.PostAsJsonAsync("/remittance/confirm", Confirm(false));
        var noKey = await client.PostAsJsonAsync("/remittance/confirm", Confirm());

        // Assert
        noApproval.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        noKey.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string.Join(",", noKey.Headers.GetValues("Access-Control-Expose-Headers")).Should().Contain("X-Transfer-Execution",
            "cross-origin clients must be able to distinguish a definitive pre-execution rejection from an uncertain result");
    }

    [Fact]
    public async Task Detail_Should_DenyPersonalUserAndReturn404ForAnUnknownOrderToStaff()
    {
        // Arrange
        var personal = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("PersonalUser").WithPermissions("Payment.Read"));
        var staff = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("Operations").WithPermissions("Payment.Read"));
        var path = $"/remittance/{Guid.NewGuid()}";

        // Act
        var denied = await personal.GetAsync(path);
        var missing = await staff.GetAsync(path);

        // Assert
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
