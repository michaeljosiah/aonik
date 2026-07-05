using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.Api.Tests;

/// <summary>
/// HTTP-level E2E for the Spec 045 payment-log endpoints — the surface the
/// CLI's <c>payment-logs</c> group drives. Real FastEndpoints pipeline +
/// UserPolicy + validation + InMemory store via CustomWebApplicationFactory.
/// </summary>
public class PaymentLogEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PaymentLogEndpointsTests(CustomWebApplicationFactory factory)
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

    private static async Task<Guid> CreateCareEntityAsync(HttpClient client, string name = "Mum")
    {
        var response = await client.PostAsJsonAsync(
            "/personal-finance/care-entities",
            new CreateCareEntityRequest("person", null, name, "NG", null, null, null, null));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CareEntityResponse>();
        return created!.Id;
    }

    private static CreatePaymentLogRequest LogRequest(
        Guid careEntityId,
        decimal amount = 200m,
        string currency = "GBP",
        Guid? idempotencyKey = null)
        => new(careEntityId, null, null, amount, currency, null,
            new DateTime(2026, 5, 28), "bank", "manual", null, idempotencyKey);

    [Fact]
    public async Task CreateLog_ThenGetAndList_RoundTrips()
    {
        var client = await CreateUserClientAsync();
        var entityId = await CreateCareEntityAsync(client);

        var createResponse = await client.PostAsJsonAsync("/personal-finance/payment-logs", LogRequest(entityId));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentLogResponse>();
        created!.CareEntityId.Should().Be(entityId);
        created.Amount.Should().Be(200m);
        created.Currency.Should().Be("GBP");

        var getResponse = await client.GetAsync($"/personal-finance/payment-logs/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await client.GetAsync($"/personal-finance/payment-logs?careEntityId={entityId}");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResponse.Content.ReadFromJsonAsync<PaymentLogListResponse>();
        list!.Items.Should().ContainSingle(p => p.Id == created.Id);
    }

    [Fact]
    public async Task Create_IsIdempotent_OnIdempotencyKey()
    {
        var client = await CreateUserClientAsync();
        var entityId = await CreateCareEntityAsync(client);
        var key = Guid.NewGuid();

        var first = await (await client.PostAsJsonAsync("/personal-finance/payment-logs", LogRequest(entityId, idempotencyKey: key)))
            .Content.ReadFromJsonAsync<PaymentLogResponse>();
        var second = await (await client.PostAsJsonAsync("/personal-finance/payment-logs", LogRequest(entityId, idempotencyKey: key)))
            .Content.ReadFromJsonAsync<PaymentLogResponse>();

        second!.Id.Should().Be(first!.Id);
    }

    [Fact]
    public async Task YearSummary_Returns_PerCurrencyTotals()
    {
        var client = await CreateUserClientAsync();
        var entityId = await CreateCareEntityAsync(client);

        await client.PostAsJsonAsync("/personal-finance/payment-logs", LogRequest(entityId, 200m, "GBP"));
        await client.PostAsJsonAsync("/personal-finance/payment-logs", LogRequest(entityId, 85000m, "NGN"));

        var summaryResponse = await client.GetAsync("/personal-finance/summary/year?year=2026");
        summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await summaryResponse.Content.ReadFromJsonAsync<YearSummary>();

        summary!.Year.Should().Be(2026);
        summary.Totals.Should().Contain(t => t.Currency == "GBP" && t.Total == 200m);
        summary.Totals.Should().Contain(t => t.Currency == "NGN" && t.Total == 85000m);
        summary.EntityCount.Should().Be(1);
    }

    [Fact]
    public async Task SoftDelete_Returns204_Excludes_Then_RestoreBringsBack()
    {
        var client = await CreateUserClientAsync();
        var entityId = await CreateCareEntityAsync(client);
        var created = await (await client.PostAsJsonAsync("/personal-finance/payment-logs", LogRequest(entityId)))
            .Content.ReadFromJsonAsync<PaymentLogResponse>();

        var deleteResponse = await client.DeleteAsync($"/personal-finance/payment-logs/{created!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await (await client.GetAsync($"/personal-finance/payment-logs?careEntityId={entityId}"))
            .Content.ReadFromJsonAsync<PaymentLogListResponse>();
        afterDelete!.Items.Should().BeEmpty();

        var restoreResponse = await client.PostAsJsonAsync($"/personal-finance/payment-logs/{created.Id}/restore", new { });
        restoreResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterRestore = await (await client.GetAsync($"/personal-finance/payment-logs?careEntityId={entityId}"))
            .Content.ReadFromJsonAsync<PaymentLogListResponse>();
        afterRestore!.Items.Should().ContainSingle(p => p.Id == created.Id);
    }

    [Fact]
    public async Task Create_WithBadChannel_Returns422()
    {
        var client = await CreateUserClientAsync();
        var entityId = await CreateCareEntityAsync(client);
        var bad = new CreatePaymentLogRequest(entityId, null, null, 200m, "GBP", null,
            new DateTime(2026, 5, 28), "crypto", "manual", null, null);

        var response = await client.PostAsJsonAsync("/personal-finance/payment-logs", bad);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ForUnownedCareEntity_Returns422()
    {
        var client = await CreateUserClientAsync();

        var response = await client.PostAsJsonAsync("/personal-finance/payment-logs", LogRequest(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Isolation_OtherUserCannotReadLog_Returns404()
    {
        var tenant = Guid.NewGuid();
        var ownerClient = await CreateUserClientAsync(tenant);
        var strangerClient = await CreateUserClientAsync(tenant);

        var entityId = await CreateCareEntityAsync(ownerClient);
        var created = await (await ownerClient.PostAsJsonAsync("/personal-finance/payment-logs", LogRequest(entityId)))
            .Content.ReadFromJsonAsync<PaymentLogResponse>();

        var strangerGet = await strangerClient.GetAsync($"/personal-finance/payment-logs/{created!.Id}");

        strangerGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/personal-finance/payment-logs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
