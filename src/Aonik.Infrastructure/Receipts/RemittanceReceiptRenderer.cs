using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

using Aonik.Finance.Contracts.Models.Remittance;
using Aonik.Finance.Contracts.Services.Remittance;
using Aonik.Finance.Services.Remittance;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Infrastructure.Receipts;

internal sealed class RemittanceReceiptRenderer : IRemittanceReceiptRenderer
{
    private static readonly object FontLock = new();

    public RemittanceReceiptFile Render(RemittanceOrderResponse transfer, string businessName, Guid tenantId, DateTime generatedAt)
    {
        lock (FontLock)
        {
            if (GlobalFontSettings.FontResolver is not ReceiptFontResolver)
                GlobalFontSettings.FontResolver = new ReceiptFontResolver(GlobalFontSettings.FontResolver);
        }
        using var document = new PdfDocument();
        document.Info.Title = $"Transfer receipt - {transfer.OrderNumber}";
        document.Info.Author = businessName;
        document.Info.CreationDate = DateTime.SpecifyKind(generatedAt, DateTimeKind.Utc);
        var normal = new XFont("AonikReceipt", 9.5, XFontStyleEx.Regular);
        var bold = new XFont("AonikReceipt", 10, XFontStyleEx.Bold);
        var ink = new XSolidBrush(XColor.FromArgb(31, 35, 40));
        var muted = new XSolidBrush(XColor.FromArgb(95, 102, 112));
        var teal = new XSolidBrush(XColor.FromArgb(5, 90, 96));
        var border = new XPen(XColor.FromArgb(228, 230, 233), 0.7);
        PdfPage page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        XGraphics graphics = XGraphics.FromPdfPage(page);
        const double margin = 42, width = 511;
        double y = margin;

        void Text(string value, double x, double top, XFont? font = null, XBrush? brush = null)
        {
            font ??= normal;
            value = Regex.Replace(value, @"\s+", " ").Trim();
            if (GlyphHelper.GlyphIndicesFromString(value, font).Any(pair => pair.GlyphIndex == 0))
                throw new InvalidStateException("Some receipt text is not supported by the bundled font. Contact support to export this receipt.");
            graphics.DrawString(value, font, brush ?? ink, new XPoint(x, top), XStringFormats.TopLeft);
        }
        List<string> Wrap(string value, double limit, XFont? font = null)
        {
            font ??= normal;
            var lines = new List<string>();
            var line = "";
            foreach (var word in Regex.Replace(value, @"\s+", " ").Trim().Split(' '))
            {
                var candidate = line.Length == 0 ? word : $"{line} {word}";
                if (graphics.MeasureString(candidate, font).Width <= limit) { line = candidate; continue; }
                if (line.Length > 0) { lines.Add(line); line = ""; }
                foreach (var character in word)
                {
                    if (line.Length > 0 && graphics.MeasureString(line + character, font).Width > limit) { lines.Add(line); line = ""; }
                    line += character;
                }
            }
            if (line.Length > 0) lines.Add(line);
            return lines.Count > 0 ? lines : [""];
        }
        void Ensure(double space)
        {
            if (y + space < page.Height.Point - 74) return;
            graphics.Dispose();page = document.AddPage();page.Size = PdfSharp.PageSize.A4;
            graphics = XGraphics.FromPdfPage(page);y = margin;
            Text("Transfer receipt - continued", margin, y, new XFont("AonikReceipt", 15, XFontStyleEx.Bold));y += 24;
            foreach (var line in Wrap($"Order {transfer.OrderNumber}", width)) { Text(line, margin, y, brush: muted);y += 13; }
            y += 10;
        }
        void Paragraph(string value, double size = 9)
        {
            var font = new XFont("AonikReceipt", size);
            foreach (var line in Wrap(value, width, font)) { Ensure(size + 7);Text(line, margin, y, font, muted);y += size + 5; }
            y += 8;
        }
        void Section(string title)
        {
            Ensure(60);y += 8;Text(title.ToUpperInvariant(), margin, y, bold, teal);y += 14;
            graphics.DrawLine(border, margin, y, margin + width, y);y += 12;
        }
        void Row(string label, string? value, bool emphasize = false)
        {
            var font = emphasize ? bold : normal;
            var lines = Wrap(string.IsNullOrWhiteSpace(value) ? "Not recorded" : value, width - 145, font);
            Ensure(24);Text(label, margin, y, normal, muted);
            foreach (var line in lines) { Ensure(14);Text(line, margin + 145, y, font, emphasize ? teal : ink);y += 12; }
            y += 6;
        }

        graphics.DrawLine(new XPen(teal.Color, 3), margin, y, margin + width, y);y += 16;
        DrawSharedIcon(graphics, margin, y, teal.Color);
        Text("AONIK", margin + 31, y, new XFont("AonikReceipt", 11, XFontStyleEx.Bold), teal);y += 28;
        Text("Transfer receipt", margin, y, new XFont("AonikReceipt", 26, XFontStyleEx.Bold));y += 34;
        foreach (var line in Wrap(businessName, width, bold)) { Text(line, margin, y, bold);y += 17; }
        y += 12;Text("RECEIPT STATUS", margin, y, new XFont("AonikReceipt", 8, XFontStyleEx.Bold), muted);
        foreach (var line in Wrap(RemittanceReceiptContent.Status(transfer), width - 145, bold)) { Text(line, margin + 145, y, bold, teal);y += 14; }
        y += 6;Paragraph(RemittanceReceiptContent.StatusDescription(transfer));

        Section("Transfer amounts");
        Row("Customer sends", RemittanceReceiptContent.Amount(transfer.OriginAmount, transfer.OriginCurrency));
        Row("Fees", RemittanceReceiptContent.Amount(transfer.FeesTotal, transfer.OriginCurrency));
        Row("Total debit", RemittanceReceiptContent.Amount(transfer.TotalAmount, transfer.OriginCurrency), true);
        Row("Recipient amount", RemittanceReceiptContent.Amount(transfer.DestinationAmount, transfer.DestinationCurrency), true);
        Row("FX rate", $"1 {transfer.OriginCurrency} = {transfer.ExchangeRate.ToString(CultureInfo.InvariantCulture)} {transfer.DestinationCurrency}");
        Row("Corridor", $"{transfer.OriginCountry} to {transfer.DestinationCountry}");
        Section("Destination and execution");
        Row("Destination", $"{transfer.DestinationType} - {transfer.MaskedAccountIdentifier}");
        Row("Payout provider", transfer.ProviderCode);Row("Transmission status", transfer.TransmissionStatus);
        Row("Created", RemittanceReceiptContent.Timestamp(transfer.CreatedAt));
        Row("Submitted", RemittanceReceiptContent.Timestamp(transfer.SubmittedAt));Row("Settled", RemittanceReceiptContent.Timestamp(transfer.SettledAt));
        Section("References");
        Row("Order number", transfer.OrderNumber);Row("Order ID", transfer.OrderId.ToString());Row("Customer reference", transfer.CustomerPartyId.ToString());
        Row("Beneficiary reference", transfer.BeneficiaryPartyId?.ToString());Row("Saved destination", transfer.DestinationExternalAccountId.ToString());
        Row("Pricing quote", transfer.PricingQuoteId.ToString());Row("Payout ID", transfer.PayoutId?.ToString());
        Row("Client reference", transfer.ClientReference);Row("Provider reference", transfer.ProviderReference);Row("Workspace reference", tenantId.ToString());
        y += 5;Paragraph("A snapshot of the recorded order and payout state. Orders describe intent; payout references identify execution. No raw payout account details are included.", 8);
        graphics.Dispose();
        for (var index = 0; index < document.PageCount; index++)
        {
            page = document.Pages[index];graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            graphics.DrawLine(border, margin, page.Height.Point - 53, margin + width, page.Height.Point - 53);
            Text($"Generated {RemittanceReceiptContent.Timestamp(generatedAt)}", margin, page.Height.Point - 40, new XFont("AonikReceipt", 8), muted);
            Text($"{index + 1} / {document.PageCount}", margin + width - 25, page.Height.Point - 40, new XFont("AonikReceipt", 8), muted);
            graphics.Dispose();
        }
        using var output = new MemoryStream();document.Save(output, false);
        return new RemittanceReceiptFile(RemittanceReceiptContent.FileName(transfer), output.ToArray());
    }

    private static void DrawSharedIcon(XGraphics graphics, double x, double y, XColor color)
    {
        using var stream = typeof(RemittanceReceiptRenderer).Assembly.GetManifestResourceStream("Aonik.Receipts.TransferIcon.svg")!;
        var svg = XDocument.Load(stream);
        var pen = new XPen(color, 1.5) { LineCap = XLineCap.Round, LineJoin = XLineJoin.Round };
        foreach (var element in svg.Descendants().Where(element => element.Name.LocalName == "path"))
        {
            var tokens = Regex.Matches(element.Attribute("d")!.Value, @"[A-Za-z]|-?\d+(?:\.\d+)?").Select(match => match.Value).ToArray();
            double currentX = 0, currentY = 0;char command = 'M';var index = 0;
            while (index < tokens.Length)
            {
                if (char.IsLetter(tokens[index][0])) command = tokens[index++][0];
                var previousX = currentX;var previousY = currentY;
                var relative = char.IsLower(command);
                var value = double.Parse(tokens[index++], CultureInfo.InvariantCulture);
                switch (char.ToUpperInvariant(command))
                {
                    case 'M':case 'L':
                        currentX = (relative ? currentX : 0) + value;
                        currentY = (relative ? currentY : 0) + double.Parse(tokens[index++], CultureInfo.InvariantCulture);
                        break;
                    case 'H': currentX = (relative ? currentX : 0) + value;break;
                    case 'V': currentY = (relative ? currentY : 0) + value;break;
                    default: throw new InvalidOperationException("The receipt icon contains an unsupported vector command.");
                }
                if (char.ToUpperInvariant(command) != 'M') graphics.DrawLine(pen, x + previousX * 0.85, y + previousY * 0.85, x + currentX * 0.85, y + currentY * 0.85);
                else command = relative ? 'l' : 'L';
            }
        }
    }

    private sealed class ReceiptFontResolver(IFontResolver? previous) : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) => familyName == "AonikReceipt"
            ? new FontResolverInfo(isBold ? "DMSans-Bold" : "DMSans-Regular") : previous?.ResolveTypeface(familyName, isBold, isItalic);
        public byte[]? GetFont(string faceName)
        {
            if (faceName is not ("DMSans-Regular" or "DMSans-Bold")) return previous?.GetFont(faceName);
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Aonik.Infrastructure.Receipts.Fonts.{faceName}.ttf")!;
            using var output = new MemoryStream();stream.CopyTo(output);return output.ToArray();
        }
    }
}
