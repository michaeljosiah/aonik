using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class GetCustomerInsightSnapshotHistoryRequest
{
    public int Take { get; set; } = 20;
}

internal sealed class GetCustomerInsightSnapshotByIdRequest
{
    public Guid SnapshotId { get; set; }
}

internal sealed class GetCurrentCustomerInsightSnapshotEndpoint : EndpointWithoutRequest<CustomerInsightSnapshotResponse>
{
    private readonly ICustomerInsightSnapshotReader _reader;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GetCurrentCustomerInsightSnapshotEndpoint(
        ICustomerInsightSnapshotReader reader,
        ICurrentUserProvider currentUserProvider)
    {
        _reader = reader;
        _currentUserProvider = currentUserProvider;
    }

    public override void Configure()
    {
        Get("/personal-finance/customer-insights/current");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = _currentUserProvider.GetCurrentUserId()
            ?? throw new InvalidOperationException("Authenticated user is required.");

        var snapshot = await _reader.GetCurrentSnapshotAsync(userId, ct);
        if (snapshot is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(snapshot, ct);
    }
}

internal sealed class GetCustomerInsightSnapshotHistoryEndpoint : Endpoint<GetCustomerInsightSnapshotHistoryRequest, IReadOnlyList<CustomerInsightSnapshotHistoryItemResponse>>
{
    private readonly ICustomerInsightSnapshotReader _reader;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GetCustomerInsightSnapshotHistoryEndpoint(
        ICustomerInsightSnapshotReader reader,
        ICurrentUserProvider currentUserProvider)
    {
        _reader = reader;
        _currentUserProvider = currentUserProvider;
    }

    public override void Configure()
    {
        Get("/personal-finance/customer-insights/history");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(GetCustomerInsightSnapshotHistoryRequest req, CancellationToken ct)
    {
        var userId = _currentUserProvider.GetCurrentUserId()
            ?? throw new InvalidOperationException("Authenticated user is required.");

        var history = await _reader.GetSnapshotHistoryAsync(userId, req.Take, ct);
        await Send.OkAsync(history, ct);
    }
}

internal sealed class GetCustomerInsightSnapshotByIdEndpoint : Endpoint<GetCustomerInsightSnapshotByIdRequest, CustomerInsightSnapshotResponse>
{
    private readonly ICustomerInsightSnapshotReader _reader;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GetCustomerInsightSnapshotByIdEndpoint(
        ICustomerInsightSnapshotReader reader,
        ICurrentUserProvider currentUserProvider)
    {
        _reader = reader;
        _currentUserProvider = currentUserProvider;
    }

    public override void Configure()
    {
        Get("/personal-finance/customer-insights/{SnapshotId}");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(GetCustomerInsightSnapshotByIdRequest req, CancellationToken ct)
    {
        var userId = _currentUserProvider.GetCurrentUserId()
            ?? throw new InvalidOperationException("Authenticated user is required.");

        var snapshot = await _reader.GetSnapshotAsync(req.SnapshotId, ct);
        if (snapshot is null || snapshot.UserId != userId)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(snapshot, ct);
    }
}
