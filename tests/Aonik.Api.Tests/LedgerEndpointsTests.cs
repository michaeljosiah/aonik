using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

using Aonik.Api.Contracts.Ledger;

namespace Aonik.Api.Tests;

public class LedgerEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LedgerEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateLedgerAccount_ReturnsCreated()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithPermissions("Ledger.Write"));


        var accountRequest = new CreateLedgerAccountRequest("Revenue", "USD");
        var accountResponse = await client.PostAsJsonAsync("/ledger/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<LedgerAccountResponse>();

        var entryRequest = new AddJournalEntryRequest(
            account!.Id,
            500.00m,
            "USD",
            "REF-001",
            "Payment received");

        // Act
        var response = await client.PostAsJsonAsync("/ledger/journal-entries", entryRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var entry = await response.Content.ReadFromJsonAsync<JournalEntryResponse>();
        entry.Should().NotBeNull();
        entry!.AccountId.Should().Be(account.Id);
        entry.Amount.Should().Be(500.00m);
        entry.Reference.Should().Be("REF-001");
        entry.Description.Should().Be("Payment received");
    }

    [Fact]
    public async Task AddJournalEntry_WithMultipleEntries_ShouldCreateAll()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithPermissions("Ledger.Write"));


        var accountRequest = new CreateLedgerAccountRequest("Operations", "USD");
        var accountResponse = await client.PostAsJsonAsync("/ledger/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<LedgerAccountResponse>();

        // Act
        var entry1Response = await client.PostAsJsonAsync("/ledger/journal-entries",
            new AddJournalEntryRequest(account!.Id, 100.00m, "USD", "REF-001", "Entry 1"));
        var entry2Response = await client.PostAsJsonAsync("/ledger/journal-entries",
            new AddJournalEntryRequest(account.Id, 200.00m, "USD", "REF-002", "Entry 2"));
        var entry3Response = await client.PostAsJsonAsync("/ledger/journal-entries",
            new AddJournalEntryRequest(account.Id, -50.00m, "USD", "REF-003", "Entry 3"));

        // Assert
        entry1Response.StatusCode.Should().Be(HttpStatusCode.Created);
        entry2Response.StatusCode.Should().Be(HttpStatusCode.Created);
        entry3Response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
