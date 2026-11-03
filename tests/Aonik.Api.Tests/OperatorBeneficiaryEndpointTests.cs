using System.Net;
using System.Net.Http.Json;

using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Endpoints.Remittance;
using FluentAssertions;

namespace Aonik.Api.Tests;

public class OperatorBeneficiaryEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private static RegisterOperatorBeneficiaryRequest Register() => new(new VerifyPayoutBeneficiaryRequest(
        Guid.NewGuid(), "Bank", "NGN", "NG", "Recipient", AccountNumber: "0690000040", BankCode: "044"));

    [Theory]
    [InlineData("PersonalUser")]
    [InlineData("ReadOnly")]
    public async Task Writes_Should_DenyNonWritingStaff_EvenWithPermissions(string role)
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles(role)
            .WithPermissions("Payment.Read", "Payment.Create", "Customers.Read", "Customers.Write"));
        var customer = Guid.NewGuid();

        var register = await client.PostAsJsonAsync("/remittance/beneficiaries/verify", Register());
        var remove = await client.DeleteAsync($"/remittance/customers/{customer}/destinations/{Guid.NewGuid()}");
        var notes = await client.PatchAsJsonAsync($"/remittance/customers/{customer}/beneficiaries/{Guid.NewGuid()}/notes", new { Notes = "Support" });

        register.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        remove.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        notes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Writes_Should_RequireCustomerWritePermission()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("Operations")
            .WithPermissions("Payment.Read", "Payment.Create", "Customers.Read"));

        var response = await client.PostAsJsonAsync("/remittance/beneficiaries/verify", Register());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_Should_RequireStaffAndBothReadPermissions_AndHideUnknownCustomer()
    {
        var personal = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("PersonalUser").WithPermissions("Payment.Read", "Customers.Read"));
        var missingPermission = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("Operations").WithPermissions("Payment.Read"));
        var staff = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("ReadOnly").WithPermissions("Payment.Read", "Customers.Read"));
        var path = $"/remittance/customers/{Guid.NewGuid()}/beneficiaries";

        (await personal.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await missingPermission.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("Bank")]
    [InlineData("MobileMoney")]
    [InlineData("Wallet")]
    public async Task Register_Should_ValidateRequiredRailFields_BeforeProviderCalls(string rail)
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("Operations")
            .WithPermissions("Payment.Create", "Customers.Write"));
        var request = new RegisterOperatorBeneficiaryRequest(new VerifyPayoutBeneficiaryRequest(Guid.NewGuid(), rail, "NGN", "NG", "Recipient"));

        var response = await client.PostAsJsonAsync("/remittance/beneficiaries/verify", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Notes_Should_RejectOversizedText_BeforeMutation()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("Operations")
            .WithPermissions("Payment.Create", "Customers.Write"));

        var response = await client.PatchAsJsonAsync($"/remittance/customers/{Guid.NewGuid()}/beneficiaries/{Guid.NewGuid()}/notes", new { Notes = new string('x', 1025) });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
