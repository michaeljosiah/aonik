using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Api.Tests;

public class WorkflowEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task DraftRoutes_Should_UsePermissionsAndCorrectCreateUpdateSemantics()
    {
        // Arrange: an explicit permission grant is authoritative; no administrator role is needed.
        using HttpClient client = await ClientAsync("Workflow.Read", "Workflow.Write");
        WorkflowSaveRequest request = Draft();

        // Act
        HttpResponseMessage created = await client.PostAsJsonAsync("/ai/workflows", request);
        WorkflowGraphResponse graph = (await created.Content.ReadFromJsonAsync<WorkflowGraphResponse>())!;
        HttpResponseMessage duplicate = await client.PostAsJsonAsync("/ai/workflows", request);
        HttpResponseMessage updated = await client.PutAsJsonAsync($"/ai/workflows/{request.Slug}", request with
        {
            ExpectedVersion = graph.ExpectedVersion, Name = "Edited draft",
        });
        HttpResponseMessage unknown = await client.PutAsJsonAsync("/ai/workflows/unknown", request with { ExpectedVersion = "" });

        // Assert
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        WorkflowGraphResponse edited = (await updated.Content.ReadFromJsonAsync<WorkflowGraphResponse>())!;
        edited.Nodes.Select(node => node.Id).Should().BeEquivalentTo(graph.Nodes.Select(node => node.Id));
    }

    [Fact]
    public async Task DraftRoutes_Should_RefuseWritesWithoutTheWritePermission()
    {
        // Arrange
        using HttpClient client = await ClientAsync("Workflow.Read");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/ai/workflows", Draft());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LegacyRun_Should_NeverFallBackToASavedDraft_WithTheSameName()
    {
        // Arrange
        using HttpClient client = await ClientAsync("Workflow.Read", "Workflow.Write", "Workflow.Run");
        WorkflowSaveRequest request = Draft();
        (await client.PostAsJsonAsync("/ai/workflows", request)).StatusCode.Should().Be(HttpStatusCode.Created);

        // Act
        HttpResponseMessage ordinary = await client.PostAsJsonAsync("/ai/workflows/run",
            new WorkflowRequest { WorkflowName = request.Slug, Input = "Do not execute this draft." });
        HttpResponseMessage legacy = await client.PostAsJsonAsync("/ai/workflows/run",
            new WorkflowRequest { WorkflowName = $"legacy:{request.Slug}", Input = "No registered factory exists." });

        // Assert
        ordinary.StatusCode.Should().Be(HttpStatusCode.Conflict);
        legacy.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WorkflowRoutes_Should_RequireAuthentication()
    {
        // Arrange
        using HttpClient anonymous = factory.CreateClient();

        // Act
        HttpResponseMessage response = await anonymous.GetAsync("/ai/workflows");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PublicationRequest_Should_QueueAnExactProposal_WithoutActivatingTheDraft()
    {
        using HttpClient client = await ClientAsync("Workflow.Read", "Workflow.Write", "Workflow.Publish");
        WorkflowSaveRequest request = Draft();
        HttpResponseMessage created = await client.PostAsJsonAsync("/ai/workflows", request);
        WorkflowGraphResponse draft = (await created.Content.ReadFromJsonAsync<WorkflowGraphResponse>())!;
        WorkflowValidation validation = (await (await client.PostAsJsonAsync($"/ai/workflows/{request.Slug}/validate", new { }))
            .Content.ReadFromJsonAsync<WorkflowValidation>())!;

        HttpResponseMessage response = await client.PostAsJsonAsync($"/ai/workflows/{request.Slug}/publication-requests",
            new WorkflowGovernanceCommand(request.Slug, draft.ExpectedVersion, validation.DefinitionHash, null));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        WorkflowGovernanceRequestResult queued = (await response.Content.ReadFromJsonAsync<WorkflowGovernanceRequestResult>())!;
        queued.ProposalId.Should().NotBeEmpty();
        queued.ApprovalRequestId.Should().NotBeEmpty();
        queued.Preview.After.Should().NotBeNull();
        WorkflowGraphResponse unchanged = (await client.GetFromJsonAsync<WorkflowGraphResponse>($"/ai/workflows/{request.Slug}"))!;
        unchanged.State.Should().Be("Draft");
        unchanged.PublishedVersionId.Should().BeNull();
    }

    [Fact]
    public async Task PublicationRequest_Should_RejectMissingAuthorityAndAlteredHash()
    {
        using HttpClient client = await ClientAsync("Workflow.Read", "Workflow.Write");
        WorkflowSaveRequest request = Draft();
        WorkflowGraphResponse draft = (await (await client.PostAsJsonAsync("/ai/workflows", request))
            .Content.ReadFromJsonAsync<WorkflowGraphResponse>())!;

        HttpResponseMessage forbidden = await client.PostAsJsonAsync($"/ai/workflows/{request.Slug}/publication-requests",
            new WorkflowGovernanceCommand(request.Slug, draft.ExpectedVersion, new string('a', 64), null));

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using HttpClient publisher = await ClientAsync("Workflow.Read", "Workflow.Write", "Workflow.Publish");
        draft = (await (await publisher.PostAsJsonAsync("/ai/workflows", request)).Content.ReadFromJsonAsync<WorkflowGraphResponse>())!;
        HttpResponseMessage altered = await publisher.PostAsJsonAsync($"/ai/workflows/{request.Slug}/publication-requests",
            new WorkflowGovernanceCommand(request.Slug, draft.ExpectedVersion, new string('a', 64), null));
        altered.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GovernanceDecisions_Should_EnforcePublisherPermission_OnBothHttpApprovalPaths()
    {
        Guid tenantId = Guid.NewGuid();
        using HttpClient publisher = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId)
            .WithRoles("PersonalUser").WithPermissions("Workflow.Read", "Workflow.Write", "Workflow.Publish"));
        using HttpClient reader = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId)
            .WithRoles("PersonalUser").WithPermissions("Workflow.Read"));
        publisher.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        reader.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        WorkflowSaveRequest request = Draft();
        WorkflowGraphResponse draft = (await (await publisher.PostAsJsonAsync("/ai/workflows", request))
            .Content.ReadFromJsonAsync<WorkflowGraphResponse>())!;
        WorkflowValidation validation = (await (await publisher.PostAsJsonAsync($"/ai/workflows/{request.Slug}/validate", new { }))
            .Content.ReadFromJsonAsync<WorkflowValidation>())!;
        WorkflowGovernanceRequestResult queued = (await (await publisher.PostAsJsonAsync($"/ai/workflows/{request.Slug}/publication-requests",
            new WorkflowGovernanceCommand(request.Slug, draft.ExpectedVersion, validation.DefinitionHash, null)))
            .Content.ReadFromJsonAsync<WorkflowGovernanceRequestResult>())!;

        (await reader.PostAsJsonAsync($"/ai/proposals/{queued.ProposalId}/approve", new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await reader.PostAsJsonAsync($"/ai/proposals/{queued.ProposalId}/dismiss", new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await reader.PostAsJsonAsync($"/ai/tool-approvals/{queued.ApprovalRequestId}/decide", new { decision = "Approve" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await publisher.GetFromJsonAsync<WorkflowGraphResponse>($"/ai/workflows/{request.Slug}"))!.PublishedVersionId.Should().BeNull();
        (await publisher.PostAsJsonAsync($"/ai/tool-approvals/{queued.ApprovalRequestId}/decide", new { decision = "Approve" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await publisher.GetFromJsonAsync<WorkflowGraphResponse>($"/ai/workflows/{request.Slug}"))!.PublishedVersionId.Should().NotBeNull();
    }

    private async Task<HttpClient> ClientAsync(params string[] permissions)
    {
        Guid tenantId = Guid.NewGuid();
        HttpClient client = await factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithTenant(tenantId).WithRoles("PersonalUser").WithPermissions(permissions));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private static WorkflowSaveRequest Draft() => new("invoice-routing", "Invoice routing", "", "Draft", "v0.1", false,
        "", null, [], [new("start", "Trigger", "Start", "", "", 0, 0, "{}"), new("end", "End", "Done", "", "", 1, 0, "{}")],
        [new("start", "end", 0, "")], null);
}
