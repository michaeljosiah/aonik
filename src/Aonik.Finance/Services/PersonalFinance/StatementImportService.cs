using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class StatementImportService : IStatementImportService
{
    private const string ImportStatusUploaded = "Uploaded";
    private const string ImportStatusParsed = "Parsed";
    private const string ImportStatusApplied = "Applied";
    private const string ImportStatusFailed = "Failed";

    private const string RowStatusParsed = "Parsed";
    private const string RowStatusFailed = "Failed";
    private const string RowStatusDuplicate = "Duplicate";

    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public StatementImportService(
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<StatementImportResponse> UploadStatementAsync(
        UploadStatementImportRequest request,
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        if (fileStream == null)
        {
            throw new ArgumentNullException(nameof(fileStream));
        }

        ValidateRequiredText(request.FileName, nameof(request.FileName));
        ValidateRequiredText(request.ContentType, nameof(request.ContentType));

        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var account = await _financeDbContext.PersonalAccounts
            .FirstOrDefaultAsync(
                item => item.Id == request.PersonalAccountId
                    && item.TenantId == tenantId
                    && item.UserId == userId
                    && !item.IsArchived,
                cancellationToken);

        if (account == null)
        {
            throw new InvalidOperationException("Personal account not found or unavailable.");
        }

        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var csvContent = await reader.ReadToEndAsync(cancellationToken);

        var statementImport = new StatementImport
        {
            TenantId = tenantId,
            UserId = userId,
            PersonalAccountId = request.PersonalAccountId,
            FileName = request.FileName.Trim(),
            StorageUri = $"inline://statement-imports/{Guid.NewGuid():N}/{request.FileName.Trim()}",
            Format = "csv",
            Status = ImportStatusUploaded,
            StartedAt = DateTime.UtcNow
        };

        _financeDbContext.StatementImports.Add(statementImport);
        await _financeDbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var parsedRows = ParseCsvRows(csvContent, account.Currency, request.PersonalAccountId);
            await MarkUploadDuplicatesAsync(parsedRows, tenantId, userId, cancellationToken);

            var rows = parsedRows.Select(row => new StatementImportRow
            {
                TenantId = tenantId,
                StatementImportId = statementImport.Id,
                RowNumber = row.RowNumber,
                OccurredAtRaw = row.OccurredAtRaw,
                AmountRaw = row.AmountRaw,
                DescriptionRaw = row.DescriptionRaw,
                MerchantRaw = row.MerchantRaw,
                CurrencyRaw = row.CurrencyRaw,
                NormalizedOccurredAt = row.NormalizedOccurredAt,
                NormalizedAmount = row.NormalizedAmount,
                NormalizedCurrency = row.NormalizedCurrency,
                NormalizedDescription = row.NormalizedDescription,
                ParseStatus = row.ParseStatus,
                ErrorMessage = row.ErrorMessage,
                Fingerprint = row.Fingerprint
            }).ToList();

            if (rows.Count > 0)
            {
                _financeDbContext.StatementImportRows.AddRange(rows);
            }

            statementImport.RowsTotal = rows.Count;
            statementImport.RowsParsed = rows.Count(row => row.ParseStatus == RowStatusParsed);
            statementImport.RowsDuplicate = rows.Count(row => row.ParseStatus == RowStatusDuplicate);
            statementImport.RowsFailed = rows.Count(row => row.ParseStatus == RowStatusFailed);
            statementImport.Status = rows.Count == 0 || statementImport.RowsFailed == rows.Count
                ? ImportStatusFailed
                : ImportStatusParsed;
            statementImport.FailureReason = statementImport.Status == ImportStatusFailed
                ? "No statement rows were successfully parsed."
                : null;

            await _financeDbContext.SaveChangesAsync(cancellationToken);

            return MapImport(statementImport);
        }
        catch (Exception ex)
        {
            statementImport.Status = ImportStatusFailed;
            statementImport.FailureReason = ex.Message;
            await _financeDbContext.SaveChangesAsync(cancellationToken);
            return MapImport(statementImport);
        }
    }

    public async Task<IReadOnlyList<StatementImportResponse>> ListImportsAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var imports = await _financeDbContext.StatementImports
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return imports.Select(MapImport).ToList();
    }

    public async Task<StatementImportResponse?> GetImportAsync(
        Guid statementImportId,
        CancellationToken cancellationToken = default)
    {
        var statementImport = await GetOwnedImportAsync(statementImportId, cancellationToken);
        return statementImport == null ? null : MapImport(statementImport);
    }

    public async Task<IReadOnlyList<StatementImportRowResponse>> ListImportRowsAsync(
        Guid statementImportId,
        CancellationToken cancellationToken = default)
    {
        var statementImport = await GetOwnedImportAsync(statementImportId, cancellationToken)
            ?? throw new InvalidOperationException("Statement import not found.");

        var rows = await _financeDbContext.StatementImportRows
            .AsNoTracking()
            .Where(row => row.StatementImportId == statementImport.Id)
            .OrderBy(row => row.RowNumber)
            .ToListAsync(cancellationToken);

        return rows.Select(MapRow).ToList();
    }

    public async Task<StatementImportApplyResponse> ApplyImportAsync(
        Guid statementImportId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var statementImport = await _financeDbContext.StatementImports
            .FirstOrDefaultAsync(
                item => item.Id == statementImportId && item.TenantId == tenantId && item.UserId == userId,
                cancellationToken)
            ?? throw new InvalidOperationException("Statement import not found.");

        if (statementImport.Status == ImportStatusApplied)
        {
            return MapApplyResponse(statementImport);
        }

        if (statementImport.Status == ImportStatusFailed)
        {
            throw new InvalidOperationException("Cannot apply a failed statement import.");
        }

        var account = await _financeDbContext.PersonalAccounts
            .FirstOrDefaultAsync(
                item => item.Id == statementImport.PersonalAccountId
                    && item.TenantId == tenantId
                    && item.UserId == userId
                    && !item.IsArchived,
                cancellationToken)
            ?? throw new InvalidOperationException("Personal account not found or unavailable.");

        var rows = await _financeDbContext.StatementImportRows
            .Where(item => item.StatementImportId == statementImportId)
            .OrderBy(item => item.RowNumber)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            throw new InvalidOperationException("No statement rows found for this import.");
        }

        var rowIds = rows.Select(row => row.Id).ToList();
        var rowIdsSet = rowIds.ToHashSet();

        var importedSourceIds = await _financeDbContext.PersonalTransactions
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId
                && item.UserId == userId
                && item.SourceType == "statement_import"
                && rowIdsSet.Contains(item.SourceId))
            .Select(item => item.SourceId)
            .ToListAsync(cancellationToken);

        var importedSourceIdSet = importedSourceIds.ToHashSet();

        var rowFingerprints = rows
            .Where(row => row.ParseStatus == RowStatusParsed && !string.IsNullOrWhiteSpace(row.Fingerprint))
            .Select(row => row.Fingerprint!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var existingFingerprints = rowFingerprints.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await _financeDbContext.PersonalTransactions
                .AsNoTracking()
                .Where(item =>
                    item.TenantId == tenantId
                    && item.UserId == userId
                    && item.ImportFingerprint != null
                    && rowFingerprints.Contains(item.ImportFingerprint)
                    && !rowIdsSet.Contains(item.SourceId))
                .Select(item => item.ImportFingerprint!)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);

        foreach (var row in rows.Where(item => item.ParseStatus == RowStatusParsed))
        {
            if (importedSourceIdSet.Contains(row.Id))
            {
                continue;
            }

            if (row.NormalizedOccurredAt == null || row.NormalizedAmount == null || string.IsNullOrWhiteSpace(row.NormalizedDescription))
            {
                row.ParseStatus = RowStatusFailed;
                row.ErrorMessage = "Cannot apply row because normalized values are incomplete.";
                continue;
            }

            if (!string.IsNullOrWhiteSpace(row.Fingerprint) && existingFingerprints.Contains(row.Fingerprint))
            {
                row.ParseStatus = RowStatusDuplicate;
                row.ErrorMessage = "Duplicate transaction fingerprint.";
                continue;
            }

            var transaction = new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                PersonalAccountId = statementImport.PersonalAccountId,
                SourceType = "statement_import",
                SourceId = row.Id,
                OccurredAt = row.NormalizedOccurredAt.Value,
                Amount = row.NormalizedAmount.Value,
                Currency = NormalizeCurrency(row.NormalizedCurrency, account.Currency)!,
                Merchant = TrimNullable(row.MerchantRaw),
                Description = row.NormalizedDescription,
                Category = null,
                Confidence = 0,
                CategorisedBy = null,
                ClassificationMethod = null,
                ReviewStatus = "Pending",
                Notes = null,
                TagsJson = "[]",
                ImportFingerprint = row.Fingerprint
            };

            _financeDbContext.PersonalTransactions.Add(transaction);

            if (!string.IsNullOrWhiteSpace(row.Fingerprint))
            {
                existingFingerprints.Add(row.Fingerprint);
            }
        }

        await _financeDbContext.SaveChangesAsync(cancellationToken);

        statementImport.RowsTotal = rows.Count;
        statementImport.RowsParsed = rows.Count(item => item.ParseStatus == RowStatusParsed);
        statementImport.RowsDuplicate = rows.Count(item => item.ParseStatus == RowStatusDuplicate);
        statementImport.RowsFailed = rows.Count(item => item.ParseStatus == RowStatusFailed);
        statementImport.RowsImported = await _financeDbContext.PersonalTransactions
            .AsNoTracking()
            .CountAsync(item =>
                item.TenantId == tenantId
                && item.UserId == userId
                && item.SourceType == "statement_import"
                && rowIdsSet.Contains(item.SourceId),
                cancellationToken);
        statementImport.Status = ImportStatusApplied;
        statementImport.CompletedAt = DateTime.UtcNow;
        statementImport.FailureReason = null;

        await _financeDbContext.SaveChangesAsync(cancellationToken);

        return MapApplyResponse(statementImport);
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }

    private async Task<StatementImport?> GetOwnedImportAsync(Guid statementImportId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return await _financeDbContext.StatementImports
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == statementImportId && item.TenantId == tenantId && item.UserId == userId,
                cancellationToken);
    }

    private async Task MarkUploadDuplicatesAsync(
        IReadOnlyList<ParsedStatementImportRow> parsedRows,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var fingerprints = parsedRows
            .Where(row => row.ParseStatus == RowStatusParsed && !string.IsNullOrWhiteSpace(row.Fingerprint))
            .Select(row => row.Fingerprint!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (fingerprints.Count == 0)
        {
            return;
        }

        var existingFingerprints = (await _financeDbContext.PersonalTransactions
                .AsNoTracking()
                .Where(item =>
                    item.TenantId == tenantId
                    && item.UserId == userId
                    && item.ImportFingerprint != null
                    && fingerprints.Contains(item.ImportFingerprint))
                .Select(item => item.ImportFingerprint!)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var row in parsedRows.Where(row => row.ParseStatus == RowStatusParsed))
        {
            if (!string.IsNullOrWhiteSpace(row.Fingerprint) && existingFingerprints.Contains(row.Fingerprint))
            {
                row.ParseStatus = RowStatusDuplicate;
                row.ErrorMessage = "Duplicate transaction fingerprint.";
            }
        }
    }

    private static List<ParsedStatementImportRow> ParseCsvRows(
        string csvContent,
        string? accountCurrency,
        Guid personalAccountId)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            throw new InvalidOperationException("CSV file is empty.");
        }

        using var reader = new StringReader(csvContent);
        var headerLine = ReadFirstNonEmptyLine(reader);
        if (headerLine == null)
        {
            throw new InvalidOperationException("CSV header row is required.");
        }

        var headers = ParseCsvLine(headerLine)
            .Select(NormalizeHeader)
            .ToList();

        var dateIndex = FindHeaderIndex(headers, "date", "transactiondate", "posteddate", "occurredat");
        var amountIndex = FindHeaderIndex(headers, "amount", "transactionamount", "value");
        var descriptionIndex = FindHeaderIndex(headers, "description", "details", "memo", "narration");
        var merchantIndex = FindHeaderIndex(headers, "merchant", "payee", "vendor");
        var currencyIndex = FindHeaderIndex(headers, "currency", "ccy");

        if (dateIndex < 0 || amountIndex < 0 || descriptionIndex < 0)
        {
            throw new InvalidOperationException("CSV must contain date, amount, and description columns.");
        }

        var rows = new List<ParsedStatementImportRow>();
        var lineNumber = 1;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseCsvLine(line);

            var occurredAtRaw = GetColumnValue(values, dateIndex);
            var amountRaw = GetColumnValue(values, amountIndex);
            var descriptionRaw = GetColumnValue(values, descriptionIndex);
            var merchantRaw = GetColumnValue(values, merchantIndex);
            var currencyRaw = GetColumnValue(values, currencyIndex);

            var normalizedOccurredAt = TryParseDate(occurredAtRaw);
            var normalizedAmount = TryParseAmount(amountRaw);
            var normalizedDescription = TrimNullable(descriptionRaw);
            var normalizedCurrency = NormalizeCurrency(currencyRaw, accountCurrency);

            var errors = new List<string>();
            if (normalizedOccurredAt == null)
            {
                errors.Add("Invalid date value.");
            }

            if (normalizedAmount == null)
            {
                errors.Add("Invalid amount value.");
            }

            if (string.IsNullOrWhiteSpace(normalizedDescription))
            {
                errors.Add("Description is required.");
            }

            if (string.IsNullOrWhiteSpace(normalizedCurrency))
            {
                errors.Add("Currency is required.");
            }

            var parseStatus = errors.Count == 0 ? RowStatusParsed : RowStatusFailed;
            var fingerprint = parseStatus == RowStatusParsed
                ? ComputeFingerprint(
                    personalAccountId,
                    normalizedOccurredAt!.Value,
                    normalizedAmount!.Value,
                    normalizedDescription!,
                    merchantRaw)
                : null;

            rows.Add(new ParsedStatementImportRow
            {
                RowNumber = lineNumber,
                OccurredAtRaw = TrimNullable(occurredAtRaw),
                AmountRaw = TrimNullable(amountRaw),
                DescriptionRaw = TrimNullable(descriptionRaw),
                MerchantRaw = TrimNullable(merchantRaw),
                CurrencyRaw = TrimNullable(currencyRaw),
                NormalizedOccurredAt = normalizedOccurredAt,
                NormalizedAmount = normalizedAmount,
                NormalizedCurrency = normalizedCurrency,
                NormalizedDescription = normalizedDescription,
                ParseStatus = parseStatus,
                ErrorMessage = errors.Count == 0 ? null : string.Join(" ", errors),
                Fingerprint = fingerprint
            });
        }

        return rows;
    }

    private static string? ReadFirstNonEmptyLine(StringReader reader)
    {
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line;
            }
        }

        return null;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        values.Add(current.ToString());
        return values;
    }

    private static string? GetColumnValue(IReadOnlyList<string> values, int index)
    {
        if (index < 0 || index >= values.Count)
        {
            return null;
        }

        return values[index];
    }

    private static int FindHeaderIndex(IReadOnlyList<string> headers, params string[] candidates)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            if (candidates.Contains(headers[index], StringComparer.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static string NormalizeHeader(string header)
    {
        return new string(
            header
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
    }

    private static DateTime? TryParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return parsed;
        }

        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out parsed))
        {
            return parsed;
        }

        return null;
    }

    private static decimal? TryParseAmount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().Replace(" ", string.Empty);

        if (decimal.TryParse(
                normalized,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands | NumberStyles.AllowCurrencySymbol,
                CultureInfo.InvariantCulture,
                out var parsedAmount))
        {
            return parsedAmount;
        }

        if (decimal.TryParse(
                normalized,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands | NumberStyles.AllowCurrencySymbol,
                CultureInfo.CurrentCulture,
                out parsedAmount))
        {
            return parsedAmount;
        }

        return null;
    }

    private static string? NormalizeCurrency(string? rawCurrency, string? fallbackCurrency)
    {
        var value = TrimNullable(rawCurrency) ?? TrimNullable(fallbackCurrency);
        return value?.ToUpperInvariant();
    }

    private static string ComputeFingerprint(
        Guid personalAccountId,
        DateTime occurredAt,
        decimal amount,
        string description,
        string? merchant)
    {
        var normalized = string.Join(
            "|",
            personalAccountId.ToString("N"),
            occurredAt.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            decimal.Round(amount, 2).ToString("0.00", CultureInfo.InvariantCulture),
            NormalizeFingerprintToken(description),
            NormalizeFingerprintToken(merchant));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizeFingerprintToken(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string? TrimNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ValidateRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }
    }

    private static StatementImportResponse MapImport(StatementImport statementImport)
    {
        return new StatementImportResponse(
            statementImport.Id,
            statementImport.PersonalAccountId,
            statementImport.FileName,
            statementImport.Format,
            statementImport.Status,
            statementImport.RowsTotal,
            statementImport.RowsParsed,
            statementImport.RowsImported,
            statementImport.RowsDuplicate,
            statementImport.RowsFailed,
            statementImport.FailureReason,
            statementImport.StartedAt,
            statementImport.CompletedAt,
            statementImport.CreatedAt,
            statementImport.UpdatedAt);
    }

    private static StatementImportRowResponse MapRow(StatementImportRow row)
    {
        return new StatementImportRowResponse(
            row.Id,
            row.StatementImportId,
            row.RowNumber,
            row.OccurredAtRaw,
            row.AmountRaw,
            row.DescriptionRaw,
            row.MerchantRaw,
            row.CurrencyRaw,
            row.NormalizedOccurredAt,
            row.NormalizedAmount,
            row.NormalizedCurrency,
            row.NormalizedDescription,
            row.ParseStatus,
            row.ErrorMessage,
            row.Fingerprint,
            row.CreatedAt,
            row.UpdatedAt);
    }

    private static StatementImportApplyResponse MapApplyResponse(StatementImport statementImport)
    {
        return new StatementImportApplyResponse(
            statementImport.Id,
            statementImport.RowsImported,
            statementImport.RowsDuplicate,
            statementImport.RowsFailed,
            statementImport.Status,
            statementImport.CompletedAt);
    }

    private sealed class ParsedStatementImportRow
    {
        public int RowNumber { get; set; }
        public string? OccurredAtRaw { get; set; }
        public string? AmountRaw { get; set; }
        public string? DescriptionRaw { get; set; }
        public string? MerchantRaw { get; set; }
        public string? CurrencyRaw { get; set; }
        public DateTime? NormalizedOccurredAt { get; set; }
        public decimal? NormalizedAmount { get; set; }
        public string? NormalizedCurrency { get; set; }
        public string? NormalizedDescription { get; set; }
        public string ParseStatus { get; set; } = RowStatusFailed;
        public string? ErrorMessage { get; set; }
        public string? Fingerprint { get; set; }
    }
}
