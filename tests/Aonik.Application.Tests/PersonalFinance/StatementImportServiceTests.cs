using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.PersonalFinance;

public class StatementImportServiceTests
{
    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId)
        {
            _tenantId = tenantId;
        }

        public Guid GetCurrentTenantId() => _tenantId;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid _userId;

        public TestCurrentUserProvider(Guid userId)
        {
            _userId = userId;
        }

        public Guid? GetCurrentUserId() => _userId;

        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = _userId;
            return true;
        }
    }

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    [Fact]
    public async Task UploadStatementAsync_ShouldMarkDuplicateRows_WhenFingerprintAlreadyExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var account = new PersonalAccount
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Main Account",
            AccountType = "Bank",
            Currency = "USD",
            Status = "Active"
        };
        context.PersonalAccounts.Add(account);
        await context.SaveChangesAsync();

        var csv = "date,amount,description,merchant,currency\n2026-01-10,-12.50,Coffee,Starbucks,USD\n";
        var importFingerprintScope = ComputeImportFingerprintScope(csv);
        var fingerprint = ComputeFingerprint(account.Id, new DateTime(2026, 1, 10), -12.50m, "Coffee", "Starbucks", 1, importFingerprintScope);
        context.PersonalTransactions.Add(new PersonalTransaction
        {
            TenantId = tenantId,
            UserId = userId,
            PersonalAccountId = account.Id,
            SourceType = "statement_import",
            SourceId = Guid.NewGuid(),
            OccurredAt = new DateTime(2026, 1, 10),
            Amount = -12.50m,
            Currency = "USD",
            Merchant = "Starbucks",
            Description = "Coffee",
            TagsJson = "[]",
            ImportFingerprint = fingerprint,
            ReviewStatus = "Pending"
        });
        await context.SaveChangesAsync();

        var service = new StatementImportService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "statement.csv", "text/csv"),
            stream);

        // Assert
        result.RowsTotal.Should().Be(1);
        result.RowsDuplicate.Should().Be(1);
        result.RowsParsed.Should().Be(0);
        result.Status.Should().Be("Parsed");
    }

    [Fact]
    public async Task ApplyImportAsync_ShouldCreateTransactionsAndUpdateImportCounters_WhenRowsAreParsed()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var account = new PersonalAccount
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Main Account",
            AccountType = "Bank",
            Currency = "USD",
            Status = "Active"
        };
        context.PersonalAccounts.Add(account);
        await context.SaveChangesAsync();

        var service = new StatementImportService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        var csv = "date,amount,description,merchant,currency\n2026-01-10,-12.50,Coffee,Starbucks,USD\ninvalid-date,100,Groceries,Market,USD\n";
        using var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var uploaded = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "statement.csv", "text/csv"),
            uploadStream);

        // Act
        var applied = await service.ApplyImportAsync(uploaded.StatementImportId);

        // Assert
        applied.Status.Should().Be("Applied");
        applied.RowsImported.Should().Be(1);
        applied.RowsFailed.Should().Be(1);

        var transactions = await context.PersonalTransactions
            .Where(item => item.SourceType == "statement_import")
            .ToListAsync();

        transactions.Should().HaveCount(1);
        transactions[0].PersonalAccountId.Should().Be(account.Id);
        transactions[0].Amount.Should().Be(-12.50m);
        transactions[0].Description.Should().Be("Coffee");
    }

    [Fact]
    public async Task ApplyImportAsync_ShouldImportRepeatedSameDayRows_WhenValuesAreIdentical()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var account = new PersonalAccount
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Main Account",
            AccountType = "Bank",
            Currency = "USD",
            Status = "Active"
        };

        context.PersonalAccounts.Add(account);
        await context.SaveChangesAsync();

        var service = new StatementImportService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        var csv = "date,amount,description,merchant,currency\n2026-01-10,-12.50,Coffee,Starbucks,USD\n2026-01-10,-12.50,Coffee,Starbucks,USD\n";
        using var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var uploaded = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "repeat-statement.csv", "text/csv"),
            uploadStream);

        // Act
        var applied = await service.ApplyImportAsync(uploaded.StatementImportId);

        // Assert
        uploaded.RowsTotal.Should().Be(2);
        uploaded.RowsDuplicate.Should().Be(0);
        uploaded.RowsParsed.Should().Be(2);

        applied.RowsImported.Should().Be(2);
        applied.RowsDuplicate.Should().Be(0);
        applied.RowsFailed.Should().Be(0);

        var transactions = await context.PersonalTransactions
            .Where(item => item.SourceType == "statement_import")
            .OrderBy(item => item.CreatedAt)
            .ToListAsync();

        transactions.Should().HaveCount(2);
        transactions[0].ImportFingerprint.Should().NotBeNullOrWhiteSpace();
        transactions[1].ImportFingerprint.Should().NotBeNullOrWhiteSpace();
        transactions[0].ImportFingerprint.Should().NotBe(transactions[1].ImportFingerprint);
        transactions[0].Amount.Should().Be(-12.50m);
        transactions[1].Amount.Should().Be(-12.50m);
    }

    [Fact]
    public async Task ApplyImportAsync_ShouldImportMatchingRowsFromDifferentStatementFiles()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var account = new PersonalAccount
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Main Account",
            AccountType = "Bank",
            Currency = "USD",
            Status = "Active"
        };

        context.PersonalAccounts.Add(account);
        await context.SaveChangesAsync();

        var service = new StatementImportService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        var firstCsv = "date,amount,description,merchant,currency\n2026-01-10,-12.50,Coffee,Starbucks,USD\n2026-01-11,-5.00,Snack,Store,USD\n";
        using var firstUploadStream = new MemoryStream(Encoding.UTF8.GetBytes(firstCsv));

        var firstUpload = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "statement-a.csv", "text/csv"),
            firstUploadStream);

        await service.ApplyImportAsync(firstUpload.StatementImportId);

        var secondCsv = "date,amount,description,merchant,currency\n2026-01-10,-12.50,Coffee,Starbucks,USD\n2026-01-12,-3.25,Parking,City,USD\n";
        using var secondUploadStream = new MemoryStream(Encoding.UTF8.GetBytes(secondCsv));

        // Act
        var secondUpload = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "statement-b.csv", "text/csv"),
            secondUploadStream);

        var secondApply = await service.ApplyImportAsync(secondUpload.StatementImportId);

        // Assert
        secondUpload.RowsDuplicate.Should().Be(0);
        secondApply.RowsImported.Should().Be(2);

        var coffeeTransactions = await context.PersonalTransactions
            .Where(item =>
                item.TenantId == tenantId
                && item.UserId == userId
                && item.SourceType == "statement_import"
                && item.Description == "Coffee"
                && item.Amount == -12.50m)
            .ToListAsync();

        coffeeTransactions.Should().HaveCount(2);
    }

    private static string ComputeFingerprint(
        Guid personalAccountId,
        DateTime occurredAt,
        decimal amount,
        string description,
        string merchant,
        int occurrence,
        string importFingerprintScope)
    {
        var baseKey = string.Join(
            "|",
            personalAccountId.ToString("N"),
            occurredAt.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            decimal.Round(amount, 2).ToString("0.00", CultureInfo.InvariantCulture),
            description.Trim().ToLowerInvariant(),
            merchant.Trim().ToLowerInvariant());

        var normalized = string.Join(
            "|",
            importFingerprintScope,
            baseKey,
            occurrence.ToString("D6", CultureInfo.InvariantCulture));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeImportFingerprintScope(string csvContent)
    {
        var normalizedContent = string.Join(
            "\n",
            csvContent
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim()));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedContent));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
