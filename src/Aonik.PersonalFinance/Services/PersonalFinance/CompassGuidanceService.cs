using System.Text.Json;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

/// <summary>
/// Deterministic AONIK Compass guidance (Spec 021 §3). Computes safe-to-spend
/// from liquid balances minus protected obligations minus active plan
/// commitments — no LLM, no new persistence table — and turns guidance into
/// recommendations reusing the existing <c>Proposal</c> system.
///
/// V1 currency rule (DEC8): single-currency only. When the relevant accounts /
/// obligations / plan commitments span multiple currencies (no trusted FX
/// normalisation in V1) the service returns warning-based partial guidance
/// rather than a silently blended amount. Missing snapshots fall back to
/// on-demand generation (DEC9), then to partial guidance if data is still thin.
/// </summary>
internal sealed class CompassGuidanceService : ICompassGuidanceService
{
    /// <summary>ProposalType namespace for Compass recommendations (Spec 021 §7, DEC5A).</summary>
    internal const string CompassProposalType = "CompassRecommendation";

    private const int LookaheadDays = 30;
    private const string DefaultCurrency = "GBP";

    private static readonly HashSet<string> LiabilityAccountTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreditCard", "Loan", "Mortgage", "LineOfCredit", "StudentLoan", "AutoLoan"
    };

    private static readonly HashSet<string> NonLiquidAssetTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Investment", "Brokerage", "Retirement", "CD"
    };

    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ICustomerInsightSnapshotReader _snapshotReader;
    private readonly ICustomerInsightSnapshotService _snapshotService;
    private readonly IGoalService _goalService;
    private readonly IAgentProposalStore _proposalStore;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public CompassGuidanceService(
        PersonalFinanceDbContext dbContext,
        ICustomerInsightSnapshotReader snapshotReader,
        ICustomerInsightSnapshotService snapshotService,
        IGoalService goalService,
        IAgentProposalStore proposalStore,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _snapshotReader = snapshotReader;
        _snapshotService = snapshotService;
        _goalService = goalService;
        _proposalStore = proposalStore;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<SafeToSpendResponse> GetSafeToSpendAsync(
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var warnings = new List<string>();

        // Snapshot fallback (DEC9): Compass prefers a current snapshot; when none
        // exists it generates one on demand. Guidance still derives the number
        // deterministically from live account/obligation data below — the
        // snapshot presence is a freshness/insufficiency signal, not the source
        // of the arithmetic.
        var snapshot = await _snapshotReader.GetCurrentSnapshotAsync(userId, cancellationToken);
        if (snapshot is null)
        {
            try
            {
                snapshot = await _snapshotService.GenerateCurrentSnapshotAsync(userId, cancellationToken);
            }
            catch (Exception)
            {
                warnings.Add("No customer insight snapshot is available and one could not be generated; guidance is based on current account data only.");
            }
        }

        var accounts = await _dbContext.PersonalAccounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.UserId == userId && !a.IsArchived)
            .ToListAsync(cancellationToken);

        if (accounts.Count == 0)
        {
            warnings.Add("No accounts found; safe-to-spend cannot be computed yet. Add or link an account.");
            return Partial(DefaultCurrency, asOfDate, warnings);
        }

        var operatingCurrency = DeterminePrimaryCurrency(accounts);

        // Liquid accounts only (assets that aren't long-term holdings).
        var liquidAccounts = accounts
            .Where(a => !LiabilityAccountTypes.Contains(a.AccountType)
                        && !NonLiquidAssetTypes.Contains(a.AccountType))
            .ToList();

        var obligations = await LoadProtectedObligationsAsync(tenantId, userId, asOfDate, cancellationToken);
        var planCommitments = await LoadActivePlanCommitmentsAsync(tenantId, userId, asOfDate, cancellationToken);

        // Mixed-currency detection (DEC8): if the relevant liquid balances or
        // obligations or plan commitments are not all in one operating currency,
        // we must NOT blend — return partial guidance with a warning instead.
        var currencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var account in liquidAccounts)
        {
            if (account.CurrentBalance != 0)
            {
                currencies.Add(account.Currency);
            }
        }

        foreach (var factor in obligations)
        {
            currencies.Add(factor.Currency);
        }

        foreach (var commitment in planCommitments)
        {
            currencies.Add(commitment.Currency);
        }

        currencies.Remove(string.Empty);

        if (currencies.Count > 1)
        {
            warnings.Add(
                $"Your liquid balances and commitments span multiple currencies ({string.Join(", ", currencies.OrderBy(c => c))}). " +
                "Compass V1 does not blend currencies without trusted exchange rates, so a single safe-to-spend amount is not shown.");
            return Partial(operatingCurrency, asOfDate, warnings, obligations, planCommitments);
        }

        var liquidAssets = liquidAccounts.Sum(a => a.CurrentBalance);
        var protectedObligations = obligations.Sum(f => f.Amount);
        var planTotal = planCommitments.Sum(f => f.Amount);
        var safeToSpend = Math.Max(0m, liquidAssets - protectedObligations - planTotal);

        var factors = obligations.Concat(planCommitments)
            .OrderBy(f => f.DueDate ?? DateTime.MaxValue)
            .ToList();

        return new SafeToSpendResponse(
            LiquidAssets: liquidAssets,
            ProtectedObligations: protectedObligations,
            PlanCommitments: planTotal,
            SafeToSpend: safeToSpend,
            Currency: operatingCurrency,
            AsOfUtc: asOfDate,
            LookaheadDays: LookaheadDays,
            IsPartial: false,
            Factors: factors,
            Warnings: warnings);
    }

    public async Task<GoalGuidanceResponse> GetGoalGuidanceAsync(
        Guid goalId,
        CancellationToken cancellationToken = default)
    {
        var goal = await _goalService.GetGoalAsync(goalId, cancellationToken)
            ?? throw new InvalidOperationException($"Goal {goalId} not found.");

        var currentPlan = await GetCurrentPlanResponseAsync(goalId, cancellationToken);
        var safeToSpend = await GetSafeToSpendAsync(DateTime.UtcNow, cancellationToken);

        var warnings = new List<string>(safeToSpend.Warnings);
        if (currentPlan is null)
        {
            warnings.Add("This goal has no active Compass plan yet. Generate one to get tailored steps.");
        }

        return new GoalGuidanceResponse(goal, currentPlan, safeToSpend, warnings);
    }

    public async Task<CompassProposalResponse> CreateCompassProposalAsync(
        CreateCompassProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ActionType))
        {
            throw new ArgumentException("Compass proposal actionType is required.", nameof(request));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        // Ensure the goal belongs to the current user before proposing against it.
        var goal = await _goalService.GetGoalAsync(request.GoalId, cancellationToken)
            ?? throw new InvalidOperationException($"Goal {request.GoalId} not found.");

        var riskTier = CompassRiskTier.Normalize(request.RiskTier);
        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? goal.Currency
            : request.Currency.Trim().ToUpperInvariant();

        var proposalId = Guid.NewGuid();

        // Payload carries userId/goalId/planId (Spec 021 §7) so current-user
        // retrieval filters by linkage rather than free-text scanning.
        var payload = new CompassProposalPayload(
            UserId: userId,
            GoalId: request.GoalId,
            PlanId: request.PlanId,
            ActionType: request.ActionType.Trim(),
            Amount: request.Amount,
            Currency: currency,
            Rationale: request.Rationale ?? string.Empty,
            RiskTier: riskTier);

        var payloadJson = JsonSerializer.Serialize(payload, PayloadSerializerOptions);

        var impactSummary = $"Compass recommendation '{payload.ActionType}' for goal '{goal.Name}': "
            + $"{payload.Amount} {payload.Currency}.";

        await _proposalStore.CreateManyAsync(
            new[]
            {
                new AgentProposalCreateRequest(
                    Id: proposalId,
                    TenantId: tenantId,
                    ProposalType: CompassProposalType,
                    ProposedByAgentId: Guid.Empty,
                    AiRunId: null,
                    ImpactSummary: impactSummary,
                    RiskTier: riskTier,
                    PayloadJson: payloadJson),
            },
            cancellationToken);

        return new CompassProposalResponse(
            ProposalId: proposalId,
            GoalId: request.GoalId,
            PlanId: request.PlanId,
            ActionType: payload.ActionType,
            Amount: payload.Amount,
            Currency: payload.Currency,
            RiskTier: riskTier,
            Status: ProposalStatusProposed,
            Rationale: payload.Rationale);
    }

    public async Task<IReadOnlyList<CompassProposalResponse>> ListCompassProposalsAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        // Scope by ProposalType (store already scopes by tenant) then filter to
        // the current user via the typed payload linkage — no free-text scan.
        var proposed = await _proposalStore.ListProposedAsync(CompassProposalType, cancellationToken);

        var results = new List<CompassProposalResponse>();
        foreach (var detail in proposed)
        {
            var payload = TryParsePayload(detail.PayloadJson);
            if (payload is null || payload.UserId != userId)
            {
                continue;
            }

            results.Add(new CompassProposalResponse(
                ProposalId: detail.Id,
                GoalId: payload.GoalId,
                PlanId: payload.PlanId,
                ActionType: payload.ActionType,
                Amount: payload.Amount,
                Currency: payload.Currency,
                RiskTier: payload.RiskTier,
                Status: detail.Status,
                Rationale: payload.Rationale));
        }

        return results;
    }

    private async Task<CompassPlanResponse?> GetCurrentPlanResponseAsync(
        Guid goalId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var plan = await _dbContext.CompassPlans
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.UserId == userId
                        && p.GoalId == goalId && p.Status == CompassPlanStatus.Active)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return plan is null ? null : CompassPlanMapper.Map(plan);
    }

    // ── Obligation + plan-commitment loaders ─────────────────────────

    private async Task<List<SafeToSpendFactor>> LoadProtectedObligationsAsync(
        Guid tenantId, Guid userId, DateTime asOf, CancellationToken cancellationToken)
    {
        var cutoff = asOf.Date.AddDays(LookaheadDays);
        var factors = new List<SafeToSpendFactor>();

        var bills = await _dbContext.Bills
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.UserId == userId && b.Status == "Active"
                        && b.NextDueDate >= asOf.Date && b.NextDueDate <= cutoff
                        && b.ExpectedAmount != null)
            .ToListAsync(cancellationToken);
        factors.AddRange(bills.Select(b => new SafeToSpendFactor(
            "Bill", b.Id, b.Payee, b.ExpectedAmount!.Value, b.Currency, b.NextDueDate)));

        var recurring = await _dbContext.PersonalRecurringBills
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.UserId == userId && b.Status == "Active"
                        && b.VerificationStatus != "Rejected"
                        && b.NextDueDate >= asOf.Date && b.NextDueDate <= cutoff
                        && b.ExpectedAmount != null)
            .ToListAsync(cancellationToken);
        factors.AddRange(recurring.Select(b => new SafeToSpendFactor(
            "RecurringBill", b.Id, b.Payee, b.ExpectedAmount!.Value, b.Currency, b.NextDueDate)));

        var debts = await _dbContext.DebtRepayments
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.UserId == userId && d.Status == "Active"
                        && d.VerificationStatus != "Rejected"
                        && d.NextDueDate >= asOf.Date && d.NextDueDate <= cutoff
                        && d.ExpectedAmount != null)
            .ToListAsync(cancellationToken);
        factors.AddRange(debts.Select(d => new SafeToSpendFactor(
            "DebtRepayment", d.Id, d.CreditorName, d.ExpectedAmount!.Value, d.Currency, d.NextDueDate)));

        return factors;
    }

    /// <summary>
    /// Active-plan commitments are the suggested step amounts from each goal's
    /// active plan — money the user has signalled they intend to set aside, so
    /// Compass protects it from safe-to-spend (the differentiator vs the bare
    /// dashboard figure).
    /// </summary>
    private async Task<List<SafeToSpendFactor>> LoadActivePlanCommitmentsAsync(
        Guid tenantId, Guid userId, DateTime asOf, CancellationToken cancellationToken)
    {
        var cutoff = asOf.Date.AddDays(LookaheadDays);

        var activePlans = await _dbContext.CompassPlans
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.UserId == userId && p.Status == CompassPlanStatus.Active)
            .ToListAsync(cancellationToken);

        var factors = new List<SafeToSpendFactor>();
        foreach (var plan in activePlans)
        {
            var planResult = SafeDeserializePlan(plan.PlanJson);
            if (planResult?.Steps is null)
            {
                continue;
            }

            foreach (var step in planResult.Steps)
            {
                if (step.SuggestedAmount is not (> 0) || string.IsNullOrWhiteSpace(step.Currency))
                {
                    continue;
                }

                // Horizon window: only commitments due within the same lookahead as obligations are
                // protected from THIS safe-to-spend. Obligations are 30-day windowed, so commitments
                // must be too — otherwise a multi-month plan's full total is wrongly subtracted from
                // today's figure (the two would be summed on mismatched time bases). Undated steps are
                // treated as current intent and kept.
                if (step.TargetDate is { } due && (due.Date < asOf.Date || due.Date > cutoff))
                {
                    continue;
                }

                factors.Add(new SafeToSpendFactor(
                    "PlanCommitment", plan.GoalId, step.Title, step.SuggestedAmount.Value, step.Currency!, step.TargetDate));
            }
        }

        return factors;
    }

    private static Agents.StructuredOutputs.CompassPlanResult? SafeDeserializePlan(string planJson)
    {
        if (string.IsNullOrWhiteSpace(planJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Agents.StructuredOutputs.CompassPlanResult>(
                planJson, Agents.StructuredOutputs.CompassPlannerStructuredOutputContract.SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static SafeToSpendResponse Partial(
        string currency,
        DateTime asOf,
        IReadOnlyList<string> warnings,
        IReadOnlyList<SafeToSpendFactor>? obligations = null,
        IReadOnlyList<SafeToSpendFactor>? planCommitments = null)
    {
        var factors = (obligations ?? Array.Empty<SafeToSpendFactor>())
            .Concat(planCommitments ?? Array.Empty<SafeToSpendFactor>())
            .OrderBy(f => f.DueDate ?? DateTime.MaxValue)
            .ToList();

        return new SafeToSpendResponse(
            LiquidAssets: 0m,
            // Blended cross-currency sums are meaningless on the partial path, so the headline
            // sub-totals stay 0 (the per-currency breakdown lives in Factors). Showing a blended
            // figure here would contradict the very warning that triggered this path.
            ProtectedObligations: 0m,
            PlanCommitments: 0m,
            SafeToSpend: 0m,
            Currency: currency,
            AsOfUtc: asOf,
            LookaheadDays: LookaheadDays,
            IsPartial: true,
            Factors: factors,
            Warnings: warnings);
    }

    private static string DeterminePrimaryCurrency(List<PersonalAccount> accounts)
    {
        if (accounts.Count == 0)
        {
            return DefaultCurrency;
        }

        return accounts
            .GroupBy(a => a.Currency.ToUpperInvariant())
            .OrderByDescending(g => g.Count())
            .First()
            .Key;
    }

    private CompassProposalPayload? TryParsePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CompassProposalPayload>(payloadJson, PayloadSerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }

    private const string ProposalStatusProposed = "Proposed";

    private static readonly JsonSerializerOptions PayloadSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Typed Compass proposal payload (Spec 021 §7).</summary>
    private sealed record CompassProposalPayload(
        Guid UserId,
        Guid GoalId,
        Guid? PlanId,
        string ActionType,
        decimal Amount,
        string Currency,
        string Rationale,
        string RiskTier = "medium");
}
