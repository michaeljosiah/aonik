using System.ComponentModel;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Finance.Agents.Tools;

/// <summary>
/// The Keeper read toolset (Spec 047 §9): describe-only reads over the Simi
/// aggregates — CareEntity (Spec 043), PaymentLog (Spec 045), and commitment
/// cycles (Spec 044). The Keeper answers descriptive questions over the caller's
/// own records ("what's the Lagos flat costing me this year?") and never
/// prescribes.
/// <para>
/// These <c>simi_*</c> tools are <strong>omitted from
/// <see cref="PersonalFinanceToolApprovalManifest"/></strong>, exactly as the
/// existing <c>pf_get_*</c>/<c>pf_list_*</c> reads are — so the
/// <c>IToolApprovalGate</c> passes them through unwrapped (read-only, no
/// approval). They carry no mutating capability, so the Keeper cannot change
/// state even if prompted to. Every underlying service is tenant- + user-scoped,
/// so the Keeper only ever reads the caller's data.
/// </para>
/// </summary>
internal sealed class SimiKeeperTools
{
    private readonly ICareEntityService _careEntityService;
    private readonly ICareEntityProfileService _careEntityProfileService;
    private readonly IPaymentLogService _paymentLogService;
    private readonly IPaymentLogSummaryService _paymentLogSummaryService;
    private readonly ICommitmentService _commitmentService;

    private SimiKeeperTools(
        ICareEntityService careEntityService,
        ICareEntityProfileService careEntityProfileService,
        IPaymentLogService paymentLogService,
        IPaymentLogSummaryService paymentLogSummaryService,
        ICommitmentService commitmentService)
    {
        _careEntityService = careEntityService;
        _careEntityProfileService = careEntityProfileService;
        _paymentLogService = paymentLogService;
        _paymentLogSummaryService = paymentLogSummaryService;
        _commitmentService = commitmentService;
    }

    [Description("Lists the people and assets (care entities) the current user supports or maintains — e.g. Mum, a flat, a school. Optionally filter by kind ('person' or 'asset'), assetType, and whether to include archived entities. Returns names, kinds, and metadata only.")]
    public async Task<IReadOnlyList<CareEntityResponse>> ListCareEntities(
        [Description("Optional kind filter: 'person' or 'asset'.")] string? kind = null,
        [Description("Optional asset type filter (e.g. 'property', 'vehicle').")] string? assetType = null,
        [Description("Include archived entities (default: false).")] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        return await _careEntityService.ListAsync(kind, assetType, includeArchived, cancellationToken);
    }

    [Description("Gets the one-call profile for a care entity: the entity plus per-currency totals (never converted), open commitments, recent payment logs, and linked document references. Use this to answer 'what is X costing me?'. Returns null if the entity is not the caller's.")]
    public async Task<CareEntityProfileResponse?> GetEntityProfile(
        [Description("The care entity's unique identifier (GUID).")] Guid careEntityId,
        CancellationToken cancellationToken = default)
    {
        return await _careEntityProfileService.GetProfileAsync(careEntityId, cancellationToken);
    }

    [Description("Lists the current user's recorded payment logs (acts of support), newest first. Optionally filter by care entity, commitment, and year. Paginated. Amounts are recorded in their original currency and never converted.")]
    public async Task<PaymentLogListResponse> ListPaymentLogs(
        [Description("Optional: only logs for this care entity (GUID).")] Guid? careEntityId = null,
        [Description("Optional: only logs for this commitment (GUID).")] Guid? commitmentId = null,
        [Description("Optional: only logs in this calendar year (e.g. 2026).")] int? year = null,
        [Description("Page number (default: 1).")] int page = 1,
        [Description("Page size (default: 50).")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return await _paymentLogService.ListAsync(careEntityId, commitmentId, year, page, pageSize, cancellationToken);
    }

    [Description("Returns the current user's per-currency totals of recorded support for a calendar year, grouped by currency (never a converted grand total). Use this to answer 'how much have I sent this year?'.")]
    public async Task<YearSummary> SummariseYear(
        [Description("The calendar year (e.g. 2026).")] int year,
        CancellationToken cancellationToken = default)
    {
        return await _paymentLogSummaryService.GetYearSummaryAsync(year, cancellationToken);
    }

    [Description("Lists the per-cycle history timeline of a commitment (paid, skipped, snoozed cycles), newest first. Paginated. Returns null if the commitment is not the caller's.")]
    public async Task<IReadOnlyList<CommitmentCycleResponse>?> ListCommitmentCycles(
        [Description("The commitment's unique identifier (GUID).")] Guid commitmentId,
        [Description("Page number (default: 1).")] int page = 1,
        [Description("Page size (default: 24).")] int pageSize = 24,
        CancellationToken cancellationToken = default)
    {
        return await _commitmentService.GetCyclesAsync(commitmentId, page, pageSize, cancellationToken);
    }

    /// <summary>
    /// Builds the Keeper read tools. Registered alongside the personal-finance
    /// agent's tools and routed through the same <c>IToolApprovalGate</c>; being
    /// unclassified reads, they pass the gate unwrapped.
    /// </summary>
    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new SimiKeeperTools(
            serviceProvider.GetRequiredService<ICareEntityService>(),
            serviceProvider.GetRequiredService<ICareEntityProfileService>(),
            serviceProvider.GetRequiredService<IPaymentLogService>(),
            serviceProvider.GetRequiredService<IPaymentLogSummaryService>(),
            serviceProvider.GetRequiredService<ICommitmentService>());

        yield return AIFunctionFactory.Create(tools.ListCareEntities, name: "simi_list_care_entities");
        yield return AIFunctionFactory.Create(tools.GetEntityProfile, name: "simi_get_entity_profile");
        yield return AIFunctionFactory.Create(tools.ListPaymentLogs, name: "simi_list_payment_logs");
        yield return AIFunctionFactory.Create(tools.SummariseYear, name: "simi_year_summary");
        yield return AIFunctionFactory.Create(tools.ListCommitmentCycles, name: "simi_list_commitment_cycles");
    }
}
