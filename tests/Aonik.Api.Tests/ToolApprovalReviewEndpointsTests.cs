using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Api.Tests;

public sealed class ToolApprovalReviewEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Theory]
    [InlineData(ToolApprovalTier.Medium)]
    [InlineData(ToolApprovalTier.High)]
    public async Task PendingApproval_Should_ExposeExactReviewToItsRequester_UntilDecided(ToolApprovalTier tier)
    {
        Guid tenantId = Guid.NewGuid();
        TestAuthOptions requester = Options(tenantId);
        using HttpClient owner = await ClientAsync(requester);
        using HttpClient otherUser = await ClientAsync(Options(tenantId));
        using HttpClient otherTenant = await ClientAsync(Options(Guid.NewGuid()));
        using HttpClient anonymous = factory.CreateClient();
        bool highRisk = tier == ToolApprovalTier.High;
        string finalStatus = highRisk ? "Captured" : "Issued";
        var preview = new ToolApprovalPreview("Synthetic finance review", JsonSerializer.SerializeToElement(new { status = "Pending" }),
            JsonSerializer.SerializeToElement(new { status = finalStatus }));
        Guid requestId;
        Guid? proposalId;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            IServiceProvider services = scope.ServiceProvider;
            services.GetRequiredService<ITenantContext>().TenantId = tenantId;
            ICurrentUserContext identity = services.GetRequiredService<ICurrentUserContext>();
            identity.UserId = requester.UserId;
            identity.TenantId = tenantId;
            identity.IsAuthenticated = true;
            ToolGateOutcome gated = await services.GetRequiredService<IToolApprovalService>().GateAsync(new(
                highRisk ? "finance_capture_payment" : "finance_issue_invoice",
                new(tier, "Synthetic finance action", highRisk ? "Finance.CapturePayment" : null),
                new Dictionary<string, object?> { [highRisk ? "paymentIntentId" : "invoiceId"] = Guid.NewGuid() }, preview));
            gated.Decision.Should().Be(highRisk ? ToolGateDecision.Queued : ToolGateDecision.PendingApproval);
            requestId = gated.ApprovalRequestId!.Value;
            proposalId = gated.ProposalId;
        }
        string path = $"/ai/tool-approvals/{requestId:D}";

        using HttpResponseMessage exact = await owner.GetAsync(path);
        exact.StatusCode.Should().Be(HttpStatusCode.OK);
        exact.Headers.CacheControl!.NoStore.Should().BeTrue();
        var review = (await exact.Content.ReadFromJsonAsync<PendingToolApprovalReview>())!;
        review.Id.Should().Be(requestId);
        review.Tier.Should().Be(tier.ToString());
        review.ProposalId.Should().Be(proposalId);
        review.Preview!.Title.Should().Be(preview.Title);
        review.Preview.After!.Value.GetProperty("status").GetString().Should().Be(finalStatus);
        (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await otherUser.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherTenant.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        string decisionPath = highRisk ? $"/ai/proposals/{proposalId:D}/dismiss" : path + "/decide";
        (await owner.PostAsJsonAsync(decisionPath, new { decision = "Reject" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        static TestAuthOptions Options(Guid tenant) => TestAuthOptions.Create().WithTenant(tenant)
            .WithRoles("PersonalUser").WithPermissions("Invoice.Read", "Invoice.Issue");

        async Task<HttpClient> ClientAsync(TestAuthOptions options)
        {
            HttpClient client = await factory.CreateAuthenticatedClientAsync(options);
            client.DefaultRequestHeaders.Add("X-Tenant-Id", options.TenantId!.Value.ToString("D"));
            return client;
        }
    }
}
