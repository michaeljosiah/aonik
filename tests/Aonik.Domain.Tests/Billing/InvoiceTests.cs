using Aonik.Domain.Billing.Entities;
using Aonik.Domain.Billing;
using FluentAssertions;

namespace Aonik.Domain.Tests.Billing;

public class InvoiceTests
{
    [Fact]
    public void Constructor_ShouldCreateInvoiceWithDraftStatus()
    {
        // Arrange & Act
        var invoice = new Invoice(
            customerId: Guid.NewGuid(),
            invoiceNumber: "INV-001",
            currency: "USD",
            dueUtc: DateTime.UtcNow.AddDays(30));

        // Assert
        invoice.Should().NotBeNull();
        invoice.Id.Should().NotBeEmpty();
        invoice.Status.Should().Be(InvoiceStatus.Draft);
        invoice.TotalAmount.Should().Be(0);
        invoice.LineItems.Should().BeEmpty();
    }

    [Fact]
    public void AddLineItem_ShouldAddItemAndUpdateTotalAmount()
    {
        // Arrange
        var invoice = new Invoice(Guid.NewGuid(), "INV-001", "USD", DateTime.UtcNow.AddDays(30));
        var lineItem = new InvoiceLineItem(invoice.Id, "Service Fee", 2, 100.00m);

        // Act
        invoice.AddLineItem(lineItem);

        // Assert
        invoice.LineItems.Should().HaveCount(1);
        invoice.TotalAmount.Should().Be(200.00m);
    }

    [Fact]
    public void AddLineItem_WithMultipleItems_ShouldCalculateCorrectTotal()
    {
        // Arrange
        var invoice = new Invoice(Guid.NewGuid(), "INV-001", "USD", DateTime.UtcNow.AddDays(30));
        var lineItem1 = new InvoiceLineItem(invoice.Id, "Service A", 2, 50.00m);
        var lineItem2 = new InvoiceLineItem(invoice.Id, "Service B", 1, 100.00m);
        var lineItem3 = new InvoiceLineItem(invoice.Id, "Service C", 3, 25.00m);

        // Act
        invoice.AddLineItem(lineItem1);
        invoice.AddLineItem(lineItem2);
        invoice.AddLineItem(lineItem3);

        // Assert
        invoice.LineItems.Should().HaveCount(3);
        invoice.TotalAmount.Should().Be(275.00m); // (2*50) + (1*100) + (3*25)
    }

    [Fact]
    public void MarkAsIssued_ShouldChangeStatusToIssued_WhenDraft()
    {
        // Arrange
        var invoice = new Invoice(Guid.NewGuid(), "INV-001", "USD", DateTime.UtcNow.AddDays(30));

        // Act
        invoice.MarkAsIssued();

        // Assert
        invoice.Status.Should().Be(InvoiceStatus.Issued);
        invoice.IssuedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MarkAsIssued_ShouldThrow_WhenNotDraft()
    {
        // Arrange
        var invoice = new Invoice(Guid.NewGuid(), "INV-001", "USD", DateTime.UtcNow.AddDays(30));
        invoice.MarkAsIssued();

        // Act
        var act = () => invoice.MarkAsIssued();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Only draft invoices can be issued");
    }

    [Fact]
    public void MarkAsPaid_ShouldChangeStatusToPaid_WhenIssued()
    {
        // Arrange
        var invoice = new Invoice(Guid.NewGuid(), "INV-001", "USD", DateTime.UtcNow.AddDays(30));
        invoice.MarkAsIssued();

        // Act
        invoice.MarkAsPaid();

        // Assert
        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public void MarkAsPaid_ShouldThrow_WhenNotIssued()
    {
        // Arrange
        var invoice = new Invoice(Guid.NewGuid(), "INV-001", "USD", DateTime.UtcNow.AddDays(30));

        // Act
        var act = () => invoice.MarkAsPaid();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Only issued invoices can be marked as paid");
    }

    [Fact]
    public void Cancel_ShouldChangeStatusToCancelled_WhenDraftOrIssued()
    {
        // Arrange
        var invoice = new Invoice(Guid.NewGuid(), "INV-001", "USD", DateTime.UtcNow.AddDays(30));
        invoice.MarkAsIssued();

        // Act
        invoice.Cancel();

        // Assert
        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldThrow_WhenPaid()
    {
        // Arrange
        var invoice = new Invoice(Guid.NewGuid(), "INV-001", "USD", DateTime.UtcNow.AddDays(30));
        invoice.MarkAsIssued();
        invoice.MarkAsPaid();

        // Act
        var act = () => invoice.Cancel();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Paid invoices cannot be cancelled");
    }
}
