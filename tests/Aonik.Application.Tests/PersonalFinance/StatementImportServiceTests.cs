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

    private sealed class NoOpGraphCacheInvalidator : IFinancialLifeGraphCacheInvalidator
    {
        public void InvalidateCurrentUserGraph()
        {
        }

        public Task InvalidateCurrentUserGraphAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InvalidateUserGraphAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InvalidateUserGraphsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InvalidateAllGraphCachesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
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
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator());

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
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator());

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
        account.CurrentBalance.Should().Be(-12.50m);
        account.BalanceAsOf.Should().NotBeNull();
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
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator());

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
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator());

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

    // ===================================================================
    // Delimiter Detection Tests
    // ===================================================================

    [Fact]
    public async Task UploadStatementAsync_ShouldParseSemicolonDelimitedCsv()
    {
        // Arrange
        var (service, account) = await CreateServiceWithAccount("USD");
        var csv = "date;amount;description;merchant;currency\n2026-02-15;-25.00;Lunch;Subway;USD\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "semi.csv", "text/csv"), stream);

        // Assert
        result.RowsTotal.Should().Be(1);
        result.RowsParsed.Should().Be(1);
        result.RowsFailed.Should().Be(0);
        result.Status.Should().Be("Parsed");
    }

    [Fact]
    public async Task UploadStatementAsync_ShouldParseTabDelimitedCsv()
    {
        // Arrange
        var (service, account) = await CreateServiceWithAccount("USD");
        var csv = "date\tamount\tdescription\tmerchant\tcurrency\n2026-03-01\t-9.99\tNetflix\tNetflix\tUSD\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "tab.tsv", "text/csv"), stream);

        // Assert
        result.RowsTotal.Should().Be(1);
        result.RowsParsed.Should().Be(1);
        result.Status.Should().Be("Parsed");
    }

    [Fact]
    public async Task UploadStatementAsync_ShouldParsePipeDelimitedCsv()
    {
        // Arrange
        var (service, account) = await CreateServiceWithAccount("USD");
        var csv = "date|amount|description|merchant|currency\n2026-04-10|-15.00|Groceries|Aldi|USD\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "pipe.csv", "text/csv"), stream);

        // Assert
        result.RowsTotal.Should().Be(1);
        result.RowsParsed.Should().Be(1);
        result.Status.Should().Be("Parsed");
    }

    // ===================================================================
    // Preamble / Metadata Skipping Tests
    // ===================================================================

    [Fact]
    public async Task UploadStatementAsync_ShouldSkipBankMetadataPreamble()
    {
        // Arrange
        var (service, account) = await CreateServiceWithAccount("USD");
        var csv = """
            Account Number: 1234567890
            Statement Period: Jan 2026 - Feb 2026
            Generated: 2026-02-28

            date,amount,description,merchant,currency
            2026-01-15,-42.00,Electric Bill,Power Co,USD
            2026-01-20,-8.50,Coffee,Starbucks,USD
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "bank-export.csv", "text/csv"), stream);

        // Assert
        result.RowsTotal.Should().Be(2);
        result.RowsParsed.Should().Be(2);
        result.RowsFailed.Should().Be(0);
        result.Status.Should().Be("Parsed");
    }

    [Fact]
    public async Task UploadStatementAsync_ShouldParseUkStyleHeaders_WithReferenceAndAmountGbp()
    {
        // Arrange
        var (service, account) = await CreateServiceWithAccount("GBP");
        var csv = """
            Account Name,Alex Morgan Current Account
            Sort Code,20-45-67

            Date,Counter Party,Reference,Type,Amount_GBP,Balance_GBP,Spending Category
            01/02/2026,Tesco,TESCO STORES 5721 LONDON,Card Payment,-42.85,2841.37,Groceries
            02/02/2026,Northstar Digital Ltd,Salary - Northstar Digital Ltd,Faster Payment,2850.00,5594.95,Income
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "uk-style.csv", "text/csv"), stream);

        var applied = await service.ApplyImportAsync(result.StatementImportId);
        var transactions = await GetTransactions(service);

        // Assert
        result.RowsTotal.Should().Be(2);
        result.RowsParsed.Should().Be(2);
        result.RowsFailed.Should().Be(0);
        result.Status.Should().Be("Parsed");

        applied.RowsImported.Should().Be(2);
        transactions.Should().HaveCount(2);
        transactions.Should().ContainSingle(item =>
            item.Merchant == "Tesco"
            && item.Description == "TESCO STORES 5721 LONDON"
            && item.Amount == -42.85m
            && item.Currency == "GBP");
        transactions.Should().ContainSingle(item =>
            item.Merchant == "Northstar Digital Ltd"
            && item.Description == "Salary - Northstar Digital Ltd"
            && item.Amount == 2850.00m
            && item.Currency == "GBP");
    }

    [Fact]
    public async Task UploadStatementAsync_ShouldFailWithClearMessage_WhenNoRecognizableHeaders()
    {
        // Arrange
        var (service, account) = await CreateServiceWithAccount("USD");
        var csv = "foo,bar,baz\n1,2,3\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "bad-headers.csv", "text/csv"), stream);

        // Assert
        result.Status.Should().Be("Failed");
        result.FailureReason.Should().Contain("header");
    }

    // ===================================================================
    // Credit / Debit Column Tests
    // ===================================================================

    [Fact]
    public async Task UploadStatementAsync_ShouldParseSeparateCreditDebitColumns()
    {
        // Arrange
        var (service, account) = await CreateServiceWithAccount("USD");
        var csv = "date,credit,debit,description,currency\n2026-01-10,100.00,,Salary,USD\n2026-01-11,,25.00,Groceries,USD\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "credit-debit.csv", "text/csv"), stream);

        // Assert
        result.RowsTotal.Should().Be(2);
        result.RowsParsed.Should().Be(2);
        result.RowsFailed.Should().Be(0);
    }

    [Fact]
    public async Task ApplyImportAsync_ShouldCorrectlySignCreditAndDebitAmounts()
    {
        // Arrange
        var (service, account) = await CreateServiceWithAccount("USD");
        var csv = "date,credit,debit,description,currency\n2026-01-10,500.00,,Salary,USD\n2026-01-11,,75.00,Groceries,USD\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var uploaded = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "credit-debit.csv", "text/csv"), stream);

        // Act
        var applied = await service.ApplyImportAsync(uploaded.StatementImportId);

        // Assert
        applied.RowsImported.Should().Be(2);

        var transactions = await GetTransactions(service);
        transactions.Should().HaveCount(2);

        var salary = transactions.First(t => t.Description == "Salary");
        salary.Amount.Should().Be(500.00m); // credit = positive

        var groceries = transactions.First(t => t.Description == "Groceries");
        groceries.Amount.Should().Be(-75.00m); // debit = negative
    }

    [Fact]
    public async Task UploadStatementAsync_ShouldFallbackToCreditDebit_WhenAmountColumnIsEmpty()
    {
        // Arrange: CSV has amount, credit, debit columns — but amount is empty for some rows
        var (service, account) = await CreateServiceWithAccount("USD");
        var csv = "date,amount,credit,debit,description,currency\n2026-01-10,,200.00,,Refund,USD\n2026-01-11,-50.00,,,Lunch,USD\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "mixed.csv", "text/csv"), stream);

        // Assert
        result.RowsTotal.Should().Be(2);
        result.RowsParsed.Should().Be(2);
        result.RowsFailed.Should().Be(0);
    }

    // ===================================================================
    // Parenthetical Negative Tests
    // ===================================================================

    [Fact]
    public async Task UploadStatementAsync_ShouldParseParentheticalNegativeAmounts()
    {
        // Arrange
        var (service, account) = await CreateServiceWithAccount("USD");
        var csv = "date,amount,description,currency\n2026-01-10,(500.00),Rent,USD\n2026-01-11,(12.50),Coffee,USD\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "parens.csv", "text/csv"), stream);

        // Assert
        result.RowsTotal.Should().Be(2);
        result.RowsParsed.Should().Be(2);
        result.RowsFailed.Should().Be(0);
    }

    [Fact]
    public async Task ApplyImportAsync_ShouldConvertParentheticalToNegativeAmounts()
    {
        // Arrange
        var (service, account) = await CreateServiceWithAccount("USD");
        var csv = "date,amount,description,currency\n2026-01-10,(500.00),Rent,USD\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var uploaded = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "parens.csv", "text/csv"), stream);

        // Act
        var applied = await service.ApplyImportAsync(uploaded.StatementImportId);

        // Assert
        applied.RowsImported.Should().Be(1);
        var transactions = await GetTransactions(service);
        transactions.Should().HaveCount(1);
        transactions[0].Amount.Should().Be(-500.00m);
    }

    // ===================================================================
    // European Number Format Tests
    // ===================================================================

    [Fact]
    public async Task UploadStatementAsync_ShouldParseEuropeanNumberFormat()
    {
        // Arrange: European format uses dot as thousands separator, comma as decimal
        var (service, account) = await CreateServiceWithAccount("EUR");
        var csv = "date;amount;description;currency\n2026-01-10;-1.234,56;Rent;EUR\n2026-01-11;42,50;Refund;EUR\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "european.csv", "text/csv"), stream);

        // Assert
        result.RowsTotal.Should().Be(2);
        result.RowsParsed.Should().Be(2);
        result.RowsFailed.Should().Be(0);
    }

    [Fact]
    public async Task ApplyImportAsync_ShouldCorrectlyParseEuropeanAmountValues()
    {
        // Arrange
        var (service, account) = await CreateServiceWithAccount("EUR");
        var csv = "date;amount;description;currency\n2026-01-10;-1.234,56;Rent;EUR\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var uploaded = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "eu.csv", "text/csv"), stream);

        // Act
        var applied = await service.ApplyImportAsync(uploaded.StatementImportId);

        // Assert
        applied.RowsImported.Should().Be(1);
        var transactions = await GetTransactions(service);
        transactions.Should().HaveCount(1);
        transactions[0].Amount.Should().Be(-1234.56m);
    }

    // ===================================================================
    // Date Format Tests
    // ===================================================================

    [Fact]
    public async Task UploadStatementAsync_ShouldParseVariousDateFormats()
    {
        // Arrange
        var (service, account) = await CreateServiceWithAccount("USD");
        var csv = """
            date,amount,description,currency
            2026-01-10,-10.00,ISO date,USD
            10/01/2026,-20.00,DD/MM/YYYY date,USD
            10-Jan-2026,-30.00,DD-MMM-YYYY date,USD
            Jan 10, 2026,-40.00,MMM DD YYYY date,USD
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "dates.csv", "text/csv"), stream);

        // Assert
        result.RowsTotal.Should().Be(4);
        result.RowsParsed.Should().BeGreaterThanOrEqualTo(3); // at least ISO + named month formats should parse
        result.RowsFailed.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task UploadStatementAsync_ShouldParseIsoDateTimeFormat()
    {
        // Arrange
        var (service, account) = await CreateServiceWithAccount("USD");
        var csv = "date,amount,description,currency\n2026-01-10T14:30:00,-10.00,With time,USD\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "datetime.csv", "text/csv"), stream);

        // Assert
        result.RowsTotal.Should().Be(1);
        result.RowsParsed.Should().Be(1);
    }

    // ===================================================================
    // Currency in Fingerprint Tests
    // ===================================================================

    [Fact]
    public async Task UploadStatementAsync_ShouldProduceDifferentFingerprints_ForDifferentCurrencies()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var usdAccount = new PersonalAccount
        {
            TenantId = tenantId, UserId = userId,
            Name = "USD Account", AccountType = "Bank", Currency = "USD", Status = "Active"
        };
        var eurAccount = new PersonalAccount
        {
            TenantId = tenantId, UserId = userId,
            Name = "EUR Account", AccountType = "Bank", Currency = "EUR", Status = "Active"
        };
        context.PersonalAccounts.AddRange(usdAccount, eurAccount);
        await context.SaveChangesAsync();

        var service = new StatementImportService(
            context, new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId), new NoOpGraphCacheInvalidator());

        var usdCsv = "date,amount,description,currency\n2026-01-10,-50.00,Transfer,USD\n";
        using var usdStream = new MemoryStream(Encoding.UTF8.GetBytes(usdCsv));

        var usdResult = await service.UploadStatementAsync(
            new UploadStatementImportRequest(usdAccount.Id, "usd.csv", "text/csv"), usdStream);
        await service.ApplyImportAsync(usdResult.StatementImportId);

        var eurCsv = "date,amount,description,currency\n2026-01-10,-50.00,Transfer,EUR\n";
        using var eurStream = new MemoryStream(Encoding.UTF8.GetBytes(eurCsv));

        // Act: same date/amount/description, different currency → should NOT be duplicate
        var eurResult = await service.UploadStatementAsync(
            new UploadStatementImportRequest(eurAccount.Id, "eur.csv", "text/csv"), eurStream);

        // Assert
        eurResult.RowsDuplicate.Should().Be(0);
        eurResult.RowsParsed.Should().Be(1);
    }

    // ===================================================================
    // File Size Guard Tests
    // ===================================================================

    [Fact]
    public async Task UploadStatementAsync_ShouldRejectOversizedFile()
    {
        // Arrange
        var (service, account) = await CreateServiceWithAccount("USD");

        // Create a stream that reports > 10MB
        var largeContent = new string('x', 11 * 1024 * 1024);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(largeContent));

        // Act
        var act = () => service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "huge.csv", "text/csv"), stream);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*maximum allowed size*");
    }

    // ===================================================================
    // Currency Symbol Stripping Tests
    // ===================================================================

    [Fact]
    public async Task UploadStatementAsync_ShouldStripCurrencySymbolsFromAmounts()
    {
        // Arrange
        var (service, account) = await CreateServiceWithAccount("USD");
        var csv = "date,amount,description,currency\n2026-01-10,$-25.00,Groceries,USD\n2026-01-11,€100.00,Refund,EUR\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "symbols.csv", "text/csv"), stream);

        // Assert
        result.RowsTotal.Should().Be(2);
        result.RowsParsed.Should().Be(2);
        result.RowsFailed.Should().Be(0);
    }

    // ===================================================================
    // Combined / Real-World Tests
    // ===================================================================

    [Fact]
    public async Task UploadStatementAsync_ShouldHandleRealWorldBankExport_WithPreambleAndSemicolons()
    {
        // Arrange: Simulates a European bank export with preamble, semicolons, and European numbers
        var (service, account) = await CreateServiceWithAccount("EUR");
        var csv = """
            Kontoauszug
            Konto: DE89 3704 0044 0532 0130 00
            Zeitraum: 01.01.2026 - 31.01.2026

            date;debit;credit;description;currency
            2026-01-05;42,50;;Einkauf REWE;EUR
            2026-01-10;;1.500,00;Gehalt;EUR
            2026-01-15;(25,00);;Parkticket;EUR
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "deutsche-bank.csv", "text/csv"), stream);

        // Assert
        result.RowsTotal.Should().Be(3);
        result.RowsParsed.Should().Be(3);
        result.RowsFailed.Should().Be(0);
        result.Status.Should().Be("Parsed");
    }

    [Fact]
    public async Task ApplyImportAsync_ShouldCorrectlySignAmounts_InRealWorldBankExport()
    {
        // Arrange
        var (service, account) = await CreateServiceWithAccount("EUR");
        var csv = """
            Bank Statement
            Account: 123456

            date;debit;credit;description;currency
            2026-01-05;100,00;;Groceries;EUR
            2026-01-10;;2.500,00;Salary;EUR
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var uploaded = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "bank.csv", "text/csv"), stream);

        // Act
        var applied = await service.ApplyImportAsync(uploaded.StatementImportId);

        // Assert
        applied.RowsImported.Should().Be(2);
        var transactions = await GetTransactions(service);
        transactions.Should().HaveCount(2);

        var groceries = transactions.First(t => t.Description == "Groceries");
        groceries.Amount.Should().Be(-100.00m); // debit = negative

        var salary = transactions.First(t => t.Description == "Salary");
        salary.Amount.Should().Be(2500.00m); // credit = positive
    }

    [Fact]
    public async Task UploadStatementAsync_ShouldHandleQuotedFieldsWithDelimiterInside()
    {
        // Arrange: CSV with commas inside quoted description fields
        var (service, account) = await CreateServiceWithAccount("USD");
        var csv = "date,amount,description,merchant,currency\n2026-01-10,-99.00,\"Electronics, cables, adapters\",Amazon,USD\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await service.UploadStatementAsync(
            new UploadStatementImportRequest(account.Id, "quoted.csv", "text/csv"), stream);

        // Assert
        result.RowsTotal.Should().Be(1);
        result.RowsParsed.Should().Be(1);
    }

    // ===================================================================
    // Helper methods
    // ===================================================================

    /// <summary>
    /// Creates a service + account for simple test scenarios that don't need
    /// direct database access for assertions.
    /// </summary>
    private async Task<(StatementImportService Service, PersonalAccount Account)> CreateServiceWithAccount(string currency)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var context = CreateDbContext(tenantId);

        var account = new PersonalAccount
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Test Account",
            AccountType = "Bank",
            Currency = currency,
            Status = "Active"
        };

        context.PersonalAccounts.Add(account);
        await context.SaveChangesAsync();

        var service = new StatementImportService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator());

        return (service, account);
    }

    /// <summary>
    /// Gets all statement_import transactions from the context associated with the service.
    /// Uses reflection to access the private field since tests need to verify transaction details.
    /// </summary>
    private static async Task<List<PersonalTransaction>> GetTransactions(StatementImportService service)
    {
        var field = typeof(StatementImportService).GetField("_financeDbContext",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var ctx = (FinanceDbContext)field.GetValue(service)!;
        return await ctx.PersonalTransactions
            .Where(t => t.SourceType == "statement_import")
            .ToListAsync();
    }

    private static string ComputeFingerprint(
        Guid personalAccountId,
        DateTime occurredAt,
        decimal amount,
        string description,
        string merchant,
        int occurrence,
        string importFingerprintScope,
        string currency = "USD")
    {
        var baseKey = string.Join(
            "|",
            personalAccountId.ToString("N"),
            occurredAt.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            decimal.Round(amount, 2).ToString("0.00", CultureInfo.InvariantCulture),
            description.Trim().ToLowerInvariant(),
            merchant.Trim().ToLowerInvariant(),
            currency.ToUpperInvariant());

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
