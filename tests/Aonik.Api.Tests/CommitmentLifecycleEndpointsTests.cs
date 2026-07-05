using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.Api.Tests;

/// <summary>
/// HTTP-level E2E for the Spec 044 Support-commitment lifecycle — author,
/// mark-done (writes a PaymentLog + rolls forward), skip, snooze, pause/resume,
/// cycle history. Real FastEndpoints pipeline + UserPolicy + validation +
/// InMemory store via CustomWebApplicationFactory.
/// </summary>
public class CommitmentLifecycleEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CommitmentLifecycleEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private Task<HttpClient> CreateUserClientAsync(Guid? tenantId = null)
    {
        var options = TestAuthOptions.Create().WithRoles("PersonalUser");
        if (tenantId.HasValue)
        {
            options.WithTenant(tenantId.Value);
        }

        return _factory.CreateAuthenticatedClientAsync(options);
    }

    private static async Task<Guid> CreateCareEntityAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/personal-finance/care-entities",
            new CreateCareEntityRequest("person", null, "Mum", "NG", null, null, null, null));
        var created = await response.Content.ReadFromJsonAsync<CareEntityResponse>();
        return created!.Id;
    }

    private static CreateSupportCommitmentRequest MonthlyAllowance(Guid entityId)
        => new(entityId, "Mum — monthly allowance", 200m, "GBP", "Monthly", 1, 28,
            null, new DateTime(2026, 5, 28), 3, null, null);

    [Fact]
    public async Task CreateSupport_ThenMarkDone_RollsForward_AndCyclesShowHistory()
    {
        var client = await CreateUserClientAsync();
        var entityId = await CreateCareEntityAsync(client);

        var createResponse = await client.PostAsJsonAsync("/personal-finance/commitments", MonthlyAllowance(entityId));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CommitmentDetail>();
        created!.CommitmentKind.Should().Be("Support");
        created.CareEntityId.Should().Be(entityId);
        created.RhythmLabel.Should().Be("Monthly · 28th");

        var doneResponse = await client.PostAsJsonAsync(
            $"/personal-finance/commitments/{created.CommitmentId}/done",
            new MarkCommitmentDoneRequest(200m, "GBP", null, null, "bank", "May", null));
        doneResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var done = await doneResponse.Content.ReadFromJsonAsync<CommitmentDetail>();
        done!.DueDate.Should().Be(new DateTime(2026, 6, 28));

        var cyclesResponse = await client.GetAsync($"/personal-finance/commitments/{created.CommitmentId}/cycles");
        cyclesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cycles = await cyclesResponse.Content.ReadFromJsonAsync<List<CommitmentCycleResponse>>();
        cycles!.Should().HaveCount(2);
        cycles.Should().Contain(c => c.DueDate == new DateTime(2026, 5, 28) && c.Status == "Paid");
        cycles.Should().Contain(c => c.DueDate == new DateTime(2026, 6, 28) && c.Status == "Open");

        var logs = await client.GetAsync($"/personal-finance/payment-logs?commitmentId={created.CommitmentId}");
        var logList = await logs.Content.ReadFromJsonAsync<PaymentLogListResponse>();
        logList!.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateSupport_WithBadRhythmUnit_Returns422()
    {
        var client = await CreateUserClientAsync();
        var entityId = await CreateCareEntityAsync(client);
        var bad = new CreateSupportCommitmentRequest(entityId, "x", 200m, "GBP", "Fortnightly", 1, null,
            null, new DateTime(2026, 5, 28), 3, null, null);

        var response = await client.PostAsJsonAsync("/personal-finance/commitments", bad);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Skip_RecordsSkippedCycle_AndAdvances()
    {
        var client = await CreateUserClientAsync();
        var entityId = await CreateCareEntityAsync(client);
        var created = await (await client.PostAsJsonAsync("/personal-finance/commitments", MonthlyAllowance(entityId)))
            .Content.ReadFromJsonAsync<CommitmentDetail>();

        var skipResponse = await client.PostAsJsonAsync(
            $"/personal-finance/commitments/{created!.CommitmentId}/skip",
            new SkipCommitmentRequest("Tight month"));
        skipResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var cycles = await (await client.GetAsync($"/personal-finance/commitments/{created.CommitmentId}/cycles"))
            .Content.ReadFromJsonAsync<List<CommitmentCycleResponse>>();
        cycles.Should().Contain(c => c.Status == "Skipped" && c.SkipReason == "Tight month");
    }

    [Fact]
    public async Task Snooze_Then_Pause_Then_Resume_AllSucceed()
    {
        var client = await CreateUserClientAsync();
        var entityId = await CreateCareEntityAsync(client);
        var created = await (await client.PostAsJsonAsync("/personal-finance/commitments", MonthlyAllowance(entityId)))
            .Content.ReadFromJsonAsync<CommitmentDetail>();
        var id = created!.CommitmentId;

        var snooze = await client.PostAsJsonAsync($"/personal-finance/commitments/{id}/snooze",
            new SnoozeCommitmentRequest(new DateTime(2027, 1, 1)));
        snooze.StatusCode.Should().Be(HttpStatusCode.OK);

        var pause = await client.PostAsJsonAsync($"/personal-finance/commitments/{id}/pause", new { });
        pause.StatusCode.Should().Be(HttpStatusCode.OK);
        (await pause.Content.ReadFromJsonAsync<CommitmentDetail>())!.Status.Should().Be("Paused");

        var resume = await client.PostAsJsonAsync($"/personal-finance/commitments/{id}/resume", new { });
        resume.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resume.Content.ReadFromJsonAsync<CommitmentDetail>())!.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Isolation_OtherUserCannotMarkDone_Returns404()
    {
        var tenant = Guid.NewGuid();
        var ownerClient = await CreateUserClientAsync(tenant);
        var strangerClient = await CreateUserClientAsync(tenant);
        var entityId = await CreateCareEntityAsync(ownerClient);
        var created = await (await ownerClient.PostAsJsonAsync("/personal-finance/commitments", MonthlyAllowance(entityId)))
            .Content.ReadFromJsonAsync<CommitmentDetail>();

        var strangerDone = await strangerClient.PostAsJsonAsync(
            $"/personal-finance/commitments/{created!.CommitmentId}/done",
            new MarkCommitmentDoneRequest(200m, "GBP", null, null, "bank", null, null));

        strangerDone.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/personal-finance/commitments");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
