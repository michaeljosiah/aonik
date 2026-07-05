using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

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
        Summary(s =>
        {
            s.Summary = "Get current customer insight snapshot";
            s.Description = "Returns the most recent customer insight snapshot containing spending patterns, category trends, and behavioural metrics.";
            s.Response(200, "Current snapshot returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "No snapshot available yet");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "Get customer insight snapshot history";
            s.Description = "Returns a paginated list of historical customer insight snapshots, allowing trend comparison over time.";
            s.Response(200, "Snapshot history returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "Get a customer insight snapshot by ID";
            s.Description = "Returns the full details of a specific historical customer insight snapshot.";
            s.Response(200, "Snapshot returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Snapshot not found");
        });
        Options(x => x.WithTags("Personal Finance"));
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
