using FluentAssertions;

using Aonik.Documents.Services;
using Aonik.SharedKernel.Abstractions.Documents;

namespace Aonik.Application.Tests.Documents;

public class FinancialDocumentGroundingTests
{
    private const string Text = "SALES INVOICE\nCustomer: Demo Customer\nInvoice: DEMO-1001\nCurrency: GBP\nConsulting: 2 x 125.00 = 250.00\nTOTAL: 250.00\nDue: 2026-12-01";
    private static FinancialDocumentFields Fields() => new("SalesInvoice", "DEMO-1001", "Demo Customer", "GBP",
        new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), [new("Consulting", 2, 125, 250)], 250, 0, 0, 250, .99, ["TOTAL: 250.00"]);

    [Fact]
    public void Grounding_Should_AcceptOnlyFieldsSupportedByTheBoundedSourceLayout()
        => FinancialDocumentValidation.GroundingIssues(Fields(), Text).Should().BeEmpty();

    [Theory]
    [InlineData("total", "SourceTotalNotGrounded")]
    [InlineData("party", "SourceIdentityNotGrounded")]
    [InlineData("evidence", "SourceEvidenceNotGrounded")]
    [InlineData("date", "SourceDueDateNotGrounded")]
    [InlineData("line", "SourceLineNotGrounded")]
    public void Grounding_Should_HoldCoherentButInventedModelFields(string change, string issue)
    {
        FinancialDocumentFields fields = change switch
        {
            "total" => Fields() with { Subtotal = 1, Total = 1, Lines = [new("Consulting", 1, 1, 1)] },
            "party" => Fields() with { CounterpartyName = "Invented Customer" },
            "evidence" => Fields() with { Evidence = ["TOTAL: 1.00"] },
            "date" => Fields() with { DueUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            _ => Fields() with { Lines = [new("Consulting", 1, 250, 250)] },
        };

        FinancialDocumentValidation.GroundingIssues(fields, Text).Should().Contain(issue);
    }

    [Theory]
    [InlineData("TOTAL: -250.00")]
    [InlineData("TOTAL: −250.00")]
    [InlineData("TOTAL: (250.00)")]
    [InlineData("TOTAL: 250.00 251.00")]
    public void Grounding_Should_NotTurnNegativeOrAmbiguousEvidenceIntoAPositiveInvoice(string total)
        => FinancialDocumentValidation.GroundingIssues(Fields(), Text.Replace("TOTAL: 250.00", total))
            .Should().Contain("SourceTotalNotGrounded");
}
