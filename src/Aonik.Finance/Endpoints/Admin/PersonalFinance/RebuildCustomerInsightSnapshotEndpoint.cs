using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Admin.PersonalFinance;

internal sealed class RebuildCustomerInsightSnapshotRequest
{
    public Guid UserId { get; set; }
}

internal sealed class RebuildCustomerInsightSnapshotEndpoint : Endpoint<RebuildCustomerInsightSnapshotRequest, CustomerInsightSnapshotResponse>
{
    private readonly ICustomerInsightSnapshotService _snapshotService;

    public RebuildCustomerInsightSnapshotEndpoint(ICustomerInsightSnapshotService snapshotService)
    {
        _snapshotService = snapshotService;
    }

    public override void Configure()
    {
        Post("/admin/personal-finance/customer-insights/rebuild/{UserId}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(RebuildCustomerInsightSnapshotRequest req, CancellationToken ct)
    {
        var userId = req.UserId == Guid.Empty ? Route<Guid>("UserId") : req.UserId;
        var snapshot = await _snapshotService.GenerateCurrentSnapshotAsync(userId, ct);
        await Send.OkAsync(snapshot, ct);
    }
}
