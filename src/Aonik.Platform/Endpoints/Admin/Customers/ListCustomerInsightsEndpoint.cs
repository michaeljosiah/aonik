using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Endpoints.Admin.Customers;

internal sealed class CustomerInsightItem
{
    public Guid Id { get; set; }
    public string SubjectType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}

internal sealed class ListCustomerInsightsResponse
{
    public IReadOnlyList<CustomerInsightItem> Items { get; set; } = [];
}

internal sealed class ListCustomerInsightsEndpoint : EndpointWithoutRequest<ListCustomerInsightsResponse>
{
    private readonly PlatformDbContext _dbContext;
    private readonly IInsightReader _insightReader;

    public ListCustomerInsightsEndpoint(PlatformDbContext dbContext, IInsightReader insightReader)
    {
        _dbContext = dbContext;
        _insightReader = insightReader;
    }

    public override void Configure()
    {
        Get("/admin/customers/{partyId}/insights");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var partyId = Route<Guid>("partyId");

        var userId = await _dbContext.UserParties
            .AsNoTracking()
            .Where(up => up.PartyId == partyId)
            .Select(up => up.UserId)
            .FirstOrDefaultAsync(ct);

        if (userId == Guid.Empty)
        {
            await Send.OkAsync(new ListCustomerInsightsResponse(), ct);
            return;
        }

        var insights = await _insightReader.ListBySubjectAsync("UserBehaviour", userId, ct);

        var items = insights.Select(i => new CustomerInsightItem
        {
            Id = i.Id,
            SubjectType = i.SubjectType,
            Title = i.Title,
            Summary = i.Summary,
            CreatedUtc = i.CreatedUtc
        }).ToList();

        await Send.OkAsync(new ListCustomerInsightsResponse { Items = items }, ct);
    }
}
