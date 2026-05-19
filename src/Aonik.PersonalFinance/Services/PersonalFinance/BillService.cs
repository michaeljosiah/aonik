using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class BillService : IBillService
{
    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public BillService(
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<BillResponse> CreateBillAsync(
        CreateBillRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredText(request.Payee, nameof(request.Payee));
        ValidateRequiredText(request.Frequency, nameof(request.Frequency));
        ValidateRequiredText(request.Currency, nameof(request.Currency));

        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var bill = new Bill
        {
            TenantId = tenantId,
            UserId = userId,
            Payee = request.Payee.Trim(),
            Frequency = request.Frequency.Trim(),
            NextDueDate = request.NextDueDate,
            ExpectedAmount = request.ExpectedAmount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Autopay = request.Autopay,
            PaidFromAccountId = request.PaidFromAccountId,
            Status = "Active"
        };

        _financeDbContext.Bills.Add(bill);
        await _financeDbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(bill);
    }

    public async Task<IReadOnlyList<BillResponse>> ListBillsAsync(
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var query = _financeDbContext.Bills
            .AsNoTracking()
            .Where(bill => bill.TenantId == tenantId && bill.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(bill => bill.Status == status);
        }

        var bills = await query
            .OrderBy(bill => bill.NextDueDate)
            .ToListAsync(cancellationToken);

        return bills.Select(MapToResponse).ToList();
    }

    public async Task<BillResponse?> GetBillAsync(
        Guid billId,
        CancellationToken cancellationToken = default)
    {
        var bill = await GetOwnedBillAsync(billId, cancellationToken);
        return bill == null ? null : MapToResponse(bill);
    }

    public async Task<BillResponse> UpdateBillAsync(
        Guid billId,
        UpdateBillRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredText(request.Payee, nameof(request.Payee));
        ValidateRequiredText(request.Frequency, nameof(request.Frequency));
        ValidateRequiredText(request.Currency, nameof(request.Currency));
        ValidateRequiredText(request.Status, nameof(request.Status));

        var bill = await GetOwnedBillAsync(billId, cancellationToken)
            ?? throw new InvalidOperationException("Bill not found.");

        bill.Payee = request.Payee.Trim();
        bill.Frequency = request.Frequency.Trim();
        bill.NextDueDate = request.NextDueDate;
        bill.ExpectedAmount = request.ExpectedAmount;
        bill.Currency = request.Currency.Trim().ToUpperInvariant();
        bill.Autopay = request.Autopay;
        bill.PaidFromAccountId = request.PaidFromAccountId;
        bill.Status = request.Status.Trim();

        await _financeDbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(bill);
    }

    public async Task ArchiveBillAsync(
        Guid billId,
        CancellationToken cancellationToken = default)
    {
        var bill = await GetOwnedBillAsync(billId, cancellationToken)
            ?? throw new InvalidOperationException("Bill not found.");

        bill.Status = "Archived";
        await _financeDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BillResponse>> GetUpcomingBillsAsync(
        int daysAhead = 7,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var now = DateTime.UtcNow.Date;
        var cutoff = now.AddDays(daysAhead);

        var bills = await _financeDbContext.Bills
            .AsNoTracking()
            .Where(bill =>
                bill.TenantId == tenantId
                && bill.UserId == userId
                && bill.Status == "Active"
                && bill.NextDueDate >= now
                && bill.NextDueDate <= cutoff)
            .OrderBy(bill => bill.NextDueDate)
            .ToListAsync(cancellationToken);

        return bills.Select(MapToResponse).ToList();
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }

    private async Task<Bill?> GetOwnedBillAsync(Guid billId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return await _financeDbContext.Bills
            .FirstOrDefaultAsync(
                bill => bill.Id == billId && bill.TenantId == tenantId && bill.UserId == userId,
                cancellationToken);
    }

    private static BillResponse MapToResponse(Bill bill)
    {
        return new BillResponse(
            bill.Id,
            bill.UserId,
            bill.Payee,
            bill.Frequency,
            bill.NextDueDate,
            bill.ExpectedAmount,
            bill.Currency,
            bill.Autopay,
            bill.PaidFromAccountId,
            bill.LinkedInvoiceId,
            bill.LinkedOrderId,
            bill.Status,
            bill.CreatedAt,
            bill.UpdatedAt);
    }

    private static void ValidateRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }
    }
}
