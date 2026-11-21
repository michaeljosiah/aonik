using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Api.Tests;

public partial class WorkflowEndpointsTests
{
    [Fact]
    public async Task TriggerRoutes_Should_SaveReadValidateAndDeleteOnlyTheSelectedTenantDraft()
    {
        using HttpClient client = await ClientAsync("Workflow.Read", "Workflow.Write", "Integration.Manage");
        WorkflowSaveRequest workflow = Draft();
        (await client.PostAsJsonAsync("/ai/workflows", workflow)).StatusCode.Should().Be(HttpStatusCode.Created);
        string path = $"/ai/workflows/{workflow.Slug}/triggers";
        var request = new SaveWorkflowTriggerRequest("Invoice created", new(1, "InvoiceCreated", Guid.NewGuid(), Guid.NewGuid(), new("Invoice", "EventSubject", []), new()));

        HttpResponseMessage created = await client.PostAsJsonAsync(path, request);
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        WorkflowTriggerDetail saved = (await created.Content.ReadFromJsonAsync<WorkflowTriggerDetail>())!;
        saved.Status.Should().Be("Draft"); saved.Active.Should().BeNull();
        (await client.GetFromJsonAsync<List<WorkflowTriggerDetail>>(path)).Should().ContainSingle(value => value.Id == saved.Id);
        (await client.PutAsJsonAsync($"{path}/{saved.Id}", request with { ExpectedVersion = "stale" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.GetFromJsonAsync<WorkflowTriggerDetail>($"{path}/{saved.Id}"))!.DraftHash.Should().Be(saved.DraftHash);
        WorkflowTriggerValidation validation = (await (await client.PostAsJsonAsync($"{path}/{saved.Id}/validate", new { })).Content.ReadFromJsonAsync<WorkflowTriggerValidation>())!;
        validation.Diagnostics.Should().Contain(value => value.Code == "TriggerNotReady");
        using HttpClient other = await ClientAsync("Workflow.Read", "Workflow.Write", "Integration.Manage");
        (await other.GetAsync($"{path}/{saved.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.DeleteAsync($"{path}/{saved.Id}?expectedVersion={Uri.EscapeDataString(saved.ExpectedVersion)}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"{path}/{saved.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TriggerRoutes_Should_RequireAuthenticationAndIntegrationAuthority()
    {
        using HttpClient anonymous = factory.CreateClient();
        (await anonymous.GetAsync("/ai/workflows/invoice-routing/triggers")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using HttpClient writer = await ClientAsync("Workflow.Read", "Workflow.Write");
        await writer.PostAsJsonAsync("/ai/workflows", Draft());
        var request = new SaveWorkflowTriggerRequest("Invoice created", new(1, "InvoiceCreated", Guid.NewGuid(), Guid.NewGuid(), new("Invoice", "EventSubject", []), new()));
        (await writer.PostAsJsonAsync("/ai/workflows/invoice-routing/triggers", request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MailboxPush_Should_RejectUnsignedAndOversizedIngressBeforeAcceptingAHint()
    {
        using HttpClient anonymous = factory.CreateClient();
        (await anonymous.PostAsJsonAsync("/integrations/gmail/notifications", new { subscription = "projects/forged/subscriptions/mail", message = new { data = "e30=", messageId = "123" } }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/integrations/gmail/notifications", new StringContent(new string('a', 40_000))))
            .StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }
}
