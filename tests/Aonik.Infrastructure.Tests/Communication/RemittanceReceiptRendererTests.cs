using System.Text;

using Aonik.Finance.Contracts.Models.Remittance;
using Aonik.Finance.Services.Remittance;
using Aonik.Infrastructure.Receipts;
using Aonik.SharedKernel.Abstractions;
using FluentAssertions;
using PdfSharp.Pdf.IO;

namespace Aonik.Infrastructure.Tests.Communication;

public sealed class RemittanceReceiptRendererTests
{
    [Fact]
    public void Render_Should_EmbedFontsAndPreserveLongReferences_WithAccuratePendingState()
    {
        var renderer = new RemittanceReceiptRenderer();
        var order = Transfer();
        var complete = renderer.Render(order, "Meridian Remit", Guid.Parse("11111111-1111-4111-8111-111111111111"), order.CreatedAt);
        var pendingOrder = order with { Status = "Transmitted", TransmissionStatus = "Pending", SettledAt = null };
        var pending = renderer.Render(pendingOrder, "Meridian Remit", Guid.NewGuid(), order.CreatedAt);
        var longReference = renderer.Render(order with { ProviderReference = string.Concat(Enumerable.Repeat("LONG-REFERENCE-", 160)) }, "Meridian Remit", Guid.NewGuid(), order.CreatedAt);

        Encoding.ASCII.GetString(complete.Content[..5]).Should().Be("%PDF-");
        using var shortPdf = PdfReader.Open(new MemoryStream(complete.Content), PdfDocumentOpenMode.Import);
        using var longPdf = PdfReader.Open(new MemoryStream(longReference.Content), PdfDocumentOpenMode.Import);
        shortPdf.PageCount.Should().Be(1);
        longPdf.PageCount.Should().BeGreaterThan(shortPdf.PageCount);
        RemittanceReceiptContent.Status(pendingOrder).Should().Be("Transmitted");
        RemittanceReceiptContent.StatusDescription(pendingOrder).Should().Contain("does not confirm recipient payment");

        if (Environment.GetEnvironmentVariable("WRITE_RECEIPT_EXAMPLES") == "1")
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aonik.sln"))) root = root.Parent;
            root.Should().NotBeNull();
            Write(Path.Combine(root!.FullName, "output/pdf/remittance-transfer-receipt-example.pdf"), complete.Content);
            Write(Path.Combine(root.FullName, "tmp/pdfs/remittance-transfer-receipt-pending.pdf"), pending.Content);
            Write(Path.Combine(root.FullName, "tmp/pdfs/remittance-transfer-receipt-long.pdf"), longReference.Content);
        }
    }

    [Fact]
    public void Render_Should_RejectUnsupportedGlyphs_InsteadOfSilentlyChangingText()
    {
        var act = () => new RemittanceReceiptRenderer().Render(Transfer(), "Unsupported \U0001F680", Guid.NewGuid(), DateTime.UtcNow);
        act.Should().Throw<InvalidStateException>();
    }

    private static void Write(string file, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, content);
    }

    private static RemittanceOrderResponse Transfer() => new(
        Guid.Parse("44444444-4444-4444-8444-444444444444"), "REM-2026-1086", "Complete",
        Guid.Parse("22222222-2222-4222-8222-222222222222"), Guid.Parse("22222222-2222-4222-8222-222222222222"),
        Guid.Parse("33333333-3333-4333-8333-333333333333"), "Bank", "•••• 4821", "GB", "NG", "GBP", "NGN",
        250m, 500000m, 3.50m, 253.50m, 2000m, Guid.Parse("55555555-5555-4555-8555-555555555555"),
        Guid.Parse("66666666-6666-4666-8666-666666666666"), "Demo provider", "REM-DEMO-1086", "PROVIDER-DEMO-1086", "Succeeded",
        new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 12, 0, 1, DateTimeKind.Utc), new DateTime(2026, 11, 1, 12, 0, 2, DateTimeKind.Utc));
}
