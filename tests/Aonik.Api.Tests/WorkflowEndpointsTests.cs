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
