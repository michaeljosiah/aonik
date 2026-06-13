using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions.Documents;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class CareEntityProfileService : ICareEntityProfileService
{
    private const int RecentLogCount = 10;

    private readonly ICareEntityService _careEntityService;
    private readonly IPaymentLogService _paymentLogService;
    private readonly ICommitmentService _commitmentService;
    private readonly IDocumentLinkReader _documentLinkReader;

    public CareEntityProfileService(
        ICareEntityService careEntityService,
        IPaymentLogService paymentLogService,
        ICommitmentService commitmentService,
        IDocumentLinkReader documentLinkReader)
    {
        _careEntityService = careEntityService;
        _paymentLogService = paymentLogService;
        _commitmentService = commitmentService;
        _documentLinkReader = documentLinkReader;
    }

    public async Task<CareEntityProfileResponse?> GetProfileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _careEntityService.GetAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        // One round-trip composition (Spec 043 §8):
        //   YearTotals + RecentLogs ← PaymentLog (Spec 045)
        //   Commitments             ← open commitments for the entity (Spec 044)
        //   Documents               ← DocumentLink via IDocumentLinkReader (Spec 046, cross-module read)
        var yearTotals = await _paymentLogService.GetEntityYearTotalsAsync(id, year: null, cancellationToken);
        var recentLogs = await _paymentLogService.GetRecentForEntityAsync(id, RecentLogCount, cancellationToken);
        var commitments = await _commitmentService.GetSummariesForEntityAsync(id, cancellationToken);

        var documentRefs = await _documentLinkReader.GetForTargetAsync("careEntity", id, cancellationToken);
        var documents = documentRefs
            .Select(d => new CareEntityDocumentRef(d.DocumentId, d.Title, d.DocumentType))
            .ToList();

        return new CareEntityProfileResponse(
            Entity: entity,
            YearTotals: yearTotals,
            Commitments: commitments,
            RecentLogs: recentLogs,
            Documents: documents);
    }
}
