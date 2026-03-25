using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class BudgetService : IBudgetService
{
    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public BudgetService(
        FinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<IReadOnlyList<BudgetCategoryResponse>> ListBudgetsAsync(
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();
        var budget = await GetOrCreateCurrentBudgetAsync(tenantId, userId, cancellationToken);
        var spentByCategory = await GetSpentByCategoryAsync(tenantId, userId, budget.PeriodStart, cancellationToken);
        return MapBudgetLines(budget.Lines, spentByCategory);
    }

    public async Task<BudgetCategoryResponse> CreateBudgetAsync(
        CreateBudgetRequest request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();
        var budget = await GetOrCreateCurrentBudgetAsync(tenantId, userId, cancellationToken);

        var categoryId = request.CategoryId?.Trim();
        var template = !string.IsNullOrEmpty(categoryId)
            ? BudgetCategoryTemplates.GetById(categoryId)
            : null;

        var line = new BudgetLine
        {
            TenantId = tenantId,
            BudgetId = budget.Id,
            Category = template?.Id ?? categoryId ?? $"budget-{budget.Lines.Count + 1}",
            LimitAmount = 0,
            Currency = "GBP",
        };

        budget.Lines.Add(line);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var spentByCategory = await GetSpentByCategoryAsync(tenantId, userId, budget.PeriodStart, cancellationToken);
        return MapBudgetLine(line, spentByCategory);
    }

    public async Task<IReadOnlyList<BudgetCategoryResponse>> UpdateBudgetAmountAsync(
        Guid budgetLineId,
        UpdateBudgetAmountRequest request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();
        var budget = await GetCurrentBudgetWithLineAsync(tenantId, userId, budgetLineId, cancellationToken);

        var line = budget.Lines.First(l => l.Id == budgetLineId);
        line.LimitAmount = request.TotalAllocated;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var spentByCategory = await GetSpentByCategoryAsync(tenantId, userId, budget.PeriodStart, cancellationToken);
        return MapBudgetLines(budget.Lines, spentByCategory);
    }

    public async Task<IReadOnlyList<BudgetCategoryResponse>> DeleteBudgetAsync(
        Guid budgetLineId,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();
        var budget = await GetCurrentBudgetWithLineAsync(tenantId, userId, budgetLineId, cancellationToken);

        var line = budget.Lines.First(l => l.Id == budgetLineId);
        budget.Lines.Remove(line);
        _dbContext.Set<BudgetLine>().Remove(line);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var spentByCategory = await GetSpentByCategoryAsync(tenantId, userId, budget.PeriodStart, cancellationToken);
        return MapBudgetLines(budget.Lines, spentByCategory);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private (Guid TenantId, Guid UserId) GetContext()
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = _currentUserProvider.GetCurrentUserId()
            ?? throw new InvalidOperationException("No authenticated user.");
        return (tenantId, userId);
    }

    private async Task<Budget> GetOrCreateCurrentBudgetAsync(
        Guid tenantId, Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var budget = await _dbContext.Set<Budget>()
            .Include(b => b.Lines)
            .FirstOrDefaultAsync(b =>
                b.TenantId == tenantId &&
                b.UserId == userId &&
                b.PeriodStart == periodStart &&
                b.Status == "Active", ct);

        if (budget != null)
            return budget;

        budget = new Budget
        {
            TenantId = tenantId,
            UserId = userId,
            PeriodType = "Monthly",
            PeriodStart = periodStart,
            BudgetCreatedBy = "User",
            Status = "Active",
        };

        _dbContext.Set<Budget>().Add(budget);
        await _dbContext.SaveChangesAsync(ct);

        return budget;
    }

    private async Task<Budget> GetCurrentBudgetWithLineAsync(
        Guid tenantId, Guid userId, Guid budgetLineId, CancellationToken ct)
    {
        var budget = await _dbContext.Set<Budget>()
            .Include(b => b.Lines)
            .FirstOrDefaultAsync(b =>
                b.TenantId == tenantId &&
                b.UserId == userId &&
                b.Status == "Active" &&
                b.Lines.Any(l => l.Id == budgetLineId), ct)
            ?? throw new InvalidOperationException("Budget line not found.");

        return budget;
    }

    private async Task<Dictionary<string, decimal>> GetSpentByCategoryAsync(
        Guid tenantId, Guid userId, DateTime periodStart, CancellationToken ct)
    {
        var periodEnd = periodStart.AddMonths(1);

        return await _dbContext.Set<PersonalTransaction>()
            .Where(t =>
                t.TenantId == tenantId &&
                t.UserId == userId &&
                t.OccurredAt >= periodStart &&
                t.OccurredAt < periodEnd &&
                t.Category != null &&
                t.Amount < 0)
            .GroupBy(t => t.Category!)
            .ToDictionaryAsync(
                g => g.Key,
                g => Math.Abs(g.Sum(t => t.Amount)),
                ct);
    }

    private static IReadOnlyList<BudgetCategoryResponse> MapBudgetLines(
        List<BudgetLine> lines,
        Dictionary<string, decimal> spentByCategory)
    {
        return lines
            .Select(l => MapBudgetLine(l, spentByCategory))
            .ToList();
    }

    private static BudgetCategoryResponse MapBudgetLine(
        BudgetLine line,
        Dictionary<string, decimal> spentByCategory)
    {
        var template = BudgetCategoryTemplates.GetById(line.Category);
        var spent = GetSpentForCategory(line.Category, template?.LinkedSpendingCategoryId, spentByCategory);

        var now = DateTime.UtcNow;
        var currentMonthLabel = now.ToString("MMM");

        return new BudgetCategoryResponse(
            Id: line.Id.ToString(),
            Name: template?.Name ?? line.Category,
            Description: template?.Description,
            IconCodePoint: template?.IconCodePoint ?? 0xef8f,
            IconFontFamily: "MaterialIcons",
            AccentRole: template?.AccentRole ?? "primary",
            LinkedSpendingCategoryId: template?.LinkedSpendingCategoryId,
            LineItems: new[]
            {
                new BudgetLineItemResponse(
                    Id: line.Id.ToString(),
                    Name: "Budget",
                    Allocated: line.LimitAmount,
                    Spent: spent)
            },
            History: GenerateEmptyHistory(currentMonthLabel));
    }

    private static decimal GetSpentForCategory(
        string category,
        string? linkedSpendingCategoryId,
        Dictionary<string, decimal> spentByCategory)
    {
        if (spentByCategory.TryGetValue(category, out var spent))
            return spent;

        if (linkedSpendingCategoryId != null &&
            spentByCategory.TryGetValue(linkedSpendingCategoryId, out spent))
            return spent;

        return 0;
    }

    private static IReadOnlyList<BudgetHistoryPointResponse> GenerateEmptyHistory(string currentMonthLabel)
    {
        string[] months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        return months.Select(m => new BudgetHistoryPointResponse(m, 0, string.Equals(m, currentMonthLabel, StringComparison.OrdinalIgnoreCase))).ToList();
    }
}
