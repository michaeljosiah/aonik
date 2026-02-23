using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

using Aonik.Finance.Contracts.Api.Ledger;

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
            TestAuthOptions.Create()
                .WithPermissions("Ledger.Write")
                .WithRoles("Operations"));

        var ledgerResponse = await client.PostAsJsonAsync("/ledger", new CreateLedgerRequest("USD"));
        var ledger = await ledgerResponse.Content.ReadFromJsonAsync<LedgerResponse>();

        var accountRequest = new CreateLedgerAccountRequest(ledger!.Id, "Revenue", "4000", "Income");
        var accountResponse = await client.PostAsJsonAsync("/ledger/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<LedgerAccountResponse>();

        var cashAccountResponse = await client.PostAsJsonAsync(
            "/ledger/accounts",
            new CreateLedgerAccountRequest(ledger.Id, "Cash", "1000", "Asset"));
        var cashAccount = await cashAccountResponse.Content.ReadFromJsonAsync<LedgerAccountResponse>();

        var entryRequest = new AddJournalEntryRequest(
            ledger.Id,
            "REF-001",
            "Payment received",
            new List<AddJournalEntryLineRequest>
            {
                new(cashAccount!.Id, "Debit", 500.00m, "USD", "Cash received"),
                new(account!.Id, "Credit", 500.00m, "USD", "Recognize revenue")
            });

        // Act
        var response = await client.PostAsJsonAsync("/ledger/journal-entries", entryRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var entry = await response.Content.ReadFromJsonAsync<JournalEntryResponse>();
        entry.Should().NotBeNull();
        entry!.LedgerId.Should().Be(ledger.Id);
        entry.Lines.Should().HaveCount(2);
        entry.Reference.Should().Be("REF-001");
        entry.Description.Should().Be("Payment received");
    }

    [Fact]
    public async Task AddJournalEntry_WithMultipleEntries_ShouldCreateAll()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create()
                .WithPermissions("Ledger.Write")
                .WithRoles("Operations"));

        var ledgerResponse = await client.PostAsJsonAsync("/ledger", new CreateLedgerRequest("USD"));
        var ledger = await ledgerResponse.Content.ReadFromJsonAsync<LedgerResponse>();

        var cashAccountResponse = await client.PostAsJsonAsync(
            "/ledger/accounts",
            new CreateLedgerAccountRequest(ledger!.Id, "Operations", "1000", "Asset"));
        var cashAccount = await cashAccountResponse.Content.ReadFromJsonAsync<LedgerAccountResponse>();

        var expenseAccountResponse = await client.PostAsJsonAsync(
            "/ledger/accounts",
            new CreateLedgerAccountRequest(ledger.Id, "Expense", "6000", "Expense"));
        var expenseAccount = await expenseAccountResponse.Content.ReadFromJsonAsync<LedgerAccountResponse>();

        // Act
        var entry1Response = await client.PostAsJsonAsync("/ledger/journal-entries",
            new AddJournalEntryRequest(
                ledger.Id,
                "REF-001",
                "Entry 1",
                new List<AddJournalEntryLineRequest>
                {
                    new(cashAccount!.Id, "Credit", 100.00m, "USD", "Cash out"),
                    new(expenseAccount!.Id, "Debit", 100.00m, "USD", "Expense")
                }));
        var entry2Response = await client.PostAsJsonAsync("/ledger/journal-entries",
            new AddJournalEntryRequest(
                ledger.Id,
                "REF-002",
                "Entry 2",
                new List<AddJournalEntryLineRequest>
                {
                    new(cashAccount!.Id, "Credit", 200.00m, "USD", "Cash out"),
                    new(expenseAccount!.Id, "Debit", 200.00m, "USD", "Expense")
                }));
        var entry3Response = await client.PostAsJsonAsync("/ledger/journal-entries",
            new AddJournalEntryRequest(
                ledger.Id,
                "REF-003",
                "Entry 3",
                new List<AddJournalEntryLineRequest>
                {
                    new(cashAccount!.Id, "Credit", 50.00m, "USD", "Cash out"),
                    new(expenseAccount!.Id, "Debit", 50.00m, "USD", "Expense")
                }));

        // Assert
        entry1Response.StatusCode.Should().Be(HttpStatusCode.Created);
        entry2Response.StatusCode.Should().Be(HttpStatusCode.Created);
        entry3Response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
