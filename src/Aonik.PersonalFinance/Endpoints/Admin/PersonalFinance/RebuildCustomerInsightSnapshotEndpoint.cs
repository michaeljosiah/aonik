using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Rebuild customer insight snapshot";
            s.Description = "Regenerates the customer insight snapshot for a given user, recalculating all financial metrics.";
            s.Response(200, "Snapshot rebuilt successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(RebuildCustomerInsightSnapshotRequest req, CancellationToken ct)
    {
        var userId = req.UserId == Guid.Empty ? Route<Guid>("UserId") : req.UserId;
        var snapshot = await _snapshotService.GenerateCurrentSnapshotAsync(userId, ct);
        await Send.OkAsync(snapshot, ct);
    }
}
