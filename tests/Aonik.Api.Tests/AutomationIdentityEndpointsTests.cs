using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Api.Tests;

public class AutomationIdentityEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Administrator_Should_ManageExactPublishedGrants_WithoutCreatingAHumanLogin()
    {
        TestAuthOptions options = TestAuthOptions.Create().WithRoles("PersonalUser").WithPermissions("Integration.Read", "Integration.Manage", "Workflow.Read",
            "Workflow.Write", "Workflow.Run", "Workflow.Publish", "Invoice.Read");
        using HttpClient client = await factory.CreateAuthenticatedClientAsync(options);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", options.TenantId.ToString());
        var draft = new WorkflowSaveRequest("automation-routing", "Automation routing", "", "Draft", "v0.1", false, "", null, [],
            [new("start", "Trigger", "Start", "", "", 0, 0, "{}"), new("end", "End", "Done", "", "", 1, 0, "{}")], [new("start", "end", 0, "")], null);
        WorkflowGraphResponse graph = (await (await client.PostAsJsonAsync("/ai/workflows", draft)).Content.ReadFromJsonAsync<WorkflowGraphResponse>())!;
        WorkflowValidation validation = (await (await client.PostAsJsonAsync($"/ai/workflows/{draft.Slug}/validate", new { })).Content.ReadFromJsonAsync<WorkflowValidation>())!;
        WorkflowGovernanceRequestResult publication = (await (await client.PostAsJsonAsync($"/ai/workflows/{draft.Slug}/publication-requests",
            new WorkflowGovernanceCommand(draft.Slug, graph.ExpectedVersion, validation.DefinitionHash, null))).Content.ReadFromJsonAsync<WorkflowGovernanceRequestResult>())!;
        (await client.PostAsJsonAsync($"/ai/tool-approvals/{publication.ApprovalRequestId}/decide", new { decision = "Approve" })).StatusCode.Should().Be(HttpStatusCode.OK);
        Guid versionId = (await client.GetFromJsonAsync<WorkflowGraphResponse>($"/ai/workflows/{draft.Slug}"))!.PublishedVersionId!.Value;
        var command = new AutomationIdentityCommand("Invoice checks", options.UserId, null, [new(versionId, "Invoice", false, [Guid.NewGuid()], [])]);

        HttpResponseMessage response = await client.PostAsJsonAsync("/v1/integrations/automation-identities", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        AutomationIdentityDetail identity = (await response.Content.ReadFromJsonAsync<AutomationIdentityDetail>())!;
        identity.ConfiguredByUserId.Should().Be(options.UserId);
        identity.Status.Should().Be("Active");
        (await client.GetFromJsonAsync<AutomationIdentityUsage>($"/v1/integrations/automation-identities/{identity.Id}/usage"))!.ActiveRuns.Should().Be(0);
        (await client.GetFromJsonAsync<AutomationWorkflowCapability>($"/v1/integrations/automation-identities/workflow-versions/{versionId}"))!.Actions.Should().BeEmpty();
        (await client.PutAsJsonAsync($"/v1/integrations/automation-identities/{identity.Id}", command with { ExpectedVersion = "stale" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var update = await client.PutAsJsonAsync($"/v1/integrations/automation-identities/{identity.Id}", command with { Name = "Reviewed checks", ExpectedVersion = identity.ExpectedVersion });
        update.StatusCode.Should().Be(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
        identity = (await update.Content.ReadFromJsonAsync<AutomationIdentityDetail>())!;
        (await client.PostAsJsonAsync($"/v1/integrations/automation-identities/{identity.Id}/disable", new { identity.ExpectedVersion })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"/v1/integrations/automation-identities/{identity.Id}", command with { ExpectedVersion = identity.ExpectedVersion })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        TestAuthOptions foreignOptions = TestAuthOptions.Create().WithRoles("PersonalUser").WithPermissions("Integration.Manage", "Workflow.Write");
        using HttpClient foreign = await factory.CreateAuthenticatedClientAsync(foreignOptions);
        foreign.DefaultRequestHeaders.Add("X-Tenant-Id", foreignOptions.TenantId.ToString());
        (await foreign.GetAsync($"/v1/integrations/automation-identities/{identity.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task IdentityEndpoints_Should_RequireAHumanAdministrator_AndBoundedTypedInput()
    {
        using HttpClient anonymous = factory.CreateClient();
        (await anonymous.GetAsync("/v1/integrations/automation-identities")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        TestAuthOptions options = TestAuthOptions.Create().WithRoles("PersonalUser").WithPermissions("Integration.Read", "Integration.Use");
        using HttpClient user = await factory.CreateAuthenticatedClientAsync(options);
        user.DefaultRequestHeaders.Add("X-Tenant-Id", options.TenantId.ToString());
        (await user.GetAsync("/v1/integrations/automation-identities")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await user.PostAsJsonAsync("/v1/integrations/automation-identities", new AutomationIdentityCommand("No authority", options.UserId, null, []))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await user.PostAsJsonAsync("/v1/integrations/automation-identities", new AutomationIdentityCommand("", Guid.Empty, null, []))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
