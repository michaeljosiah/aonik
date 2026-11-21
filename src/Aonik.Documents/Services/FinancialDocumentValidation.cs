using System.Globalization;
using System.Text.RegularExpressions;

using Aonik.SharedKernel.Abstractions.Documents;

namespace Aonik.Documents.Services;

internal static class FinancialDocumentValidation
{
    public static IReadOnlyList<string> GroundingIssues(FinancialDocumentFields fields, string text)
    {
        List<string> issues = [];
        if (fields.InvoiceNumber?.Length > 100 || fields.CounterpartyName?.Length > 200 || fields.Lines?.Count > 50
            || fields.Lines?.Any(line => line?.Description?.Length > 500) == true || fields.Evidence?.Count > 20
            || fields.Evidence?.Any(evidence => evidence?.Length > 500) == true) return ["SourceFieldsRequireReview"];
        string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 2000 || lines.Any(line => line.Length > 2000)) return ["SourceLayoutRequiresReview"];
        string normalized = Normalize(text);
        if (string.IsNullOrWhiteSpace(fields.InvoiceNumber) || !normalized.Contains(Normalize(fields.InvoiceNumber), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(fields.CounterpartyName) || !normalized.Contains(Normalize(fields.CounterpartyName), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(fields.Currency) || !normalized.Contains(fields.Currency, StringComparison.Ordinal)) issues.Add("SourceIdentityNotGrounded");
        if (fields.Kind == "SalesInvoice" && (normalized.Contains("SUPPLIER INVOICE", StringComparison.Ordinal)
            || normalized.Contains("CREDIT NOTE", StringComparison.Ordinal) || normalized.Contains("CREDIT MEMO", StringComparison.Ordinal))) issues.Add("SourceDirectionConflict");
        if (fields.Evidence is null || fields.Evidence.Any(evidence => string.IsNullOrWhiteSpace(evidence)
            || !normalized.Contains(Normalize(evidence), StringComparison.Ordinal))) issues.Add("SourceEvidenceNotGrounded");
        string[] totals = lines.Where(line => Regex.IsMatch(line.Trim(), "^(?:GRAND\\s+TOTAL|TOTAL(?:\\s+DUE)?|AMOUNT\\s+DUE)\\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)).ToArray();
        if (totals.Length == 0 || totals.Any(line => line.Contains('(') || line.Contains(')') || Numbers(line).Distinct().Count() != 1
            || !Numbers(line).Contains(fields.Total))) issues.Add("SourceTotalNotGrounded");
        if (fields.DueUtc is { } due && !lines.Any(line => Regex.IsMatch(line.Trim(), "^(?:DUE(?:\\s+DATE)?|PAY\\s+BY|PAYMENT\\s+DUE)\\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)
            && (line.Contains(due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), StringComparison.Ordinal)
                || line.Contains(due.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), StringComparison.Ordinal)))) issues.Add("SourceDueDateNotGrounded");
        if (fields.Lines is not null)
            foreach (FinancialDocumentLine line in fields.Lines.Take(51))
            {
                if (line is null || string.IsNullOrWhiteSpace(line.Description)) { issues.Add("SourceLineNotGrounded"); continue; }
                bool found = false;
                for (int index = 0; index < lines.Length && !found; index++)
                {
                    if (!Normalize(lines[index]).Contains(Normalize(line.Description), StringComparison.Ordinal)) continue;
                    IReadOnlyList<decimal> values = Numbers(lines[index]);
                    if (values.Count < 3)
                    {
                        string[] tail = lines.Skip(index + 1).Take(2).TakeWhile(value => Regex.IsMatch(string.IsNullOrEmpty(fields.Currency) ? value : value.Replace(fields.Currency, "", StringComparison.Ordinal),
                            "^[0-9\\s,.=xX*:/+\\-\\p{Sc}]+$", RegexOptions.NonBacktracking)).ToArray();
                        values = Numbers(string.Join(' ', new[] { lines[index] }.Concat(tail)));
                    }
                    found = values.Contains(line.Quantity) && values.Contains(line.UnitPrice) && values.Contains(line.Total);
                }
                if (!found) issues.Add("SourceLineNotGrounded");
            }
        return issues.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string Normalize(string value) => string.Join(' ', value.ToUpperInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static IReadOnlyList<decimal> Numbers(string value) => Regex.Matches(value.Replace('−', '-').Replace('–', '-'), "[+-]?[0-9][0-9,.]*", RegexOptions.NonBacktracking)
        .Where(match => Regex.IsMatch(match.Value, "^[+-]?[0-9]+(?:,[0-9]{3})*(?:\\.[0-9]{1,4})?$", RegexOptions.NonBacktracking))
        .Select(match => decimal.TryParse(match.Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out decimal amount)
            ? (decimal?)amount : null).Where(amount => amount.HasValue).Select(amount => amount!.Value).ToArray();

    public static IReadOnlyList<string> Issues(FinancialDocumentFields fields, DocumentTextProvenance? extractor)
    {
        List<string> issues = [];
        if (fields.Kind != "SalesInvoice") issues.Add(fields.Kind switch
        {
            "SupplierBill" => "UnsupportedTenantPayable",
            "PersonalBill" => "ExplicitPersonalOwnerRequired",
            _ => "DocumentKindUnknown",
        });
        if (!double.IsFinite(fields.Confidence) || fields.Confidence is < .90 or > 1
            || (extractor?.Confidence is { } confidence && (!double.IsFinite(confidence) || confidence < .85))) issues.Add("LowConfidence");
        if (string.IsNullOrWhiteSpace(fields.InvoiceNumber) || fields.InvoiceNumber.Length > 100
            || string.IsNullOrWhiteSpace(fields.CounterpartyName) || fields.CounterpartyName.Length > 200) issues.Add("MissingPartyOrInvoiceNumber");
        if (fields.Currency is not { Length: 3 } || fields.Currency.Any(character => !char.IsAsciiLetterUpper(character))) issues.Add("CurrencyInvalid");
        if (fields.DueUtc is not { Kind: DateTimeKind.Utc }) issues.Add("DueDateMissing");
        if (fields.Tax != 0 || fields.Discount != 0) issues.Add("UnsupportedTaxOrDiscount");
        if (fields.Lines is null || fields.Lines.Count is < 1 or > 50) issues.Add("LineItemsMissing");
        else
        {
            decimal sum = 0;
            foreach (FinancialDocumentLine line in fields.Lines)
            {
                if (line is null || string.IsNullOrWhiteSpace(line.Description) || line.Description.Length > 500
                    || line.Quantity <= 0 || line.Quantity > 1_000_000 || line.UnitPrice < 0 || line.UnitPrice > 1_000_000_000
                    || line.Quantity != decimal.Round(line.Quantity, 4) || line.UnitPrice != decimal.Round(line.UnitPrice, 4)
                    || !Enumerable.Range(0, 5).Any(scale => line.Total == decimal.Round(line.Quantity * line.UnitPrice, scale, MidpointRounding.AwayFromZero)))
                { issues.Add("LineItemInvalid"); continue; }
                sum += line.Total;
            }
            if (sum != fields.Subtotal || fields.Total != fields.Subtotal + fields.Tax - fields.Discount
                || fields.Total <= 0 || fields.Total > 1_000_000_000 || fields.Total != decimal.Round(fields.Total, 4)) issues.Add("TotalsConflict");
        }
        if (fields.Evidence is null || fields.Evidence.Count is < 1 or > 20
            || fields.Evidence.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 500)) issues.Add("EvidenceMissing");
        return issues.Distinct(StringComparer.Ordinal).ToArray();
    }
}
