using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.PersonalFinance;

// ── List Commitments ──────────────────────────────────────────────────

internal sealed class ListCommitmentsRequest
{
    [QueryParam] public string? Type { get; set; }
    [QueryParam] public string? VerificationStatus { get; set; }
    [QueryParam] public string? Status { get; set; }
    [QueryParam] public DateTime? DueFrom { get; set; }
    [QueryParam] public DateTime? DueTo { get; set; }
    [QueryParam] public Guid? AccountId { get; set; }
    [QueryParam] public string? Search { get; set; }
    [QueryParam] public int Page { get; set; } = 1;
    [QueryParam] public int PageSize { get; set; } = 20;
}

internal sealed class ListCommitmentsEndpoint
    : Endpoint<ListCommitmentsRequest, CommitmentListResponse>
{
    private readonly ICommitmentService _service;

    public ListCommitmentsEndpoint(ICommitmentService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/commitments");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List commitments";
            s.Description = "Returns a paginated, filterable list of all recurring commitments (bills, subscriptions, debt repayments) for the authenticated user.";
            s.Response(200, "Commitment list returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(ListCommitmentsRequest req, CancellationToken ct)
    {
        var filter = new CommitmentListFilter(
            Type: req.Type,
            VerificationStatus: req.VerificationStatus,
            Status: req.Status,
            DueFrom: req.DueFrom,
            DueTo: req.DueTo,
            AccountId: req.AccountId,
            Search: req.Search,
            Page: req.Page,
            PageSize: req.PageSize);

        var response = await _service.ListCommitmentsAsync(filter, ct);
        await Send.OkAsync(response, ct);
    }
}

// ── Get Commitment ────────────────────────────────────────────────────

internal sealed class GetCommitmentEndpoint
    : EndpointWithoutRequest<CommitmentDetail>
{
    private readonly ICommitmentService _service;

    public GetCommitmentEndpoint(ICommitmentService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/commitments/{commitmentId}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get commitment detail";
            s.Description = "Returns the full detail of a single commitment by ID, resolved across all source types (bill, subscription, debt repayment).";
            s.Response(200, "Commitment detail returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Commitment not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("commitmentId");
        var response = await _service.GetCommitmentAsync(id, ct);

        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}

// ── Create Commitment from Transaction ────────────────────────────────

internal sealed class CreateCommitmentFromTransactionEndpoint
    : Endpoint<CreateCommitmentFromTransactionRequest, CommitmentDetail>
{
    private readonly ICommitmentService _service;

    public CreateCommitmentFromTransactionEndpoint(ICommitmentService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/commitments/from-transaction");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Create commitment from transaction";
            s.Description = "Promotes a personal transaction into a tracked recurring commitment. Creates the underlying entity (PersonalRecurringBill, Subscription, or DebtRepayment) based on the specified commitment type.";
            s.Response(200, "Commitment created successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CreateCommitmentFromTransactionRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _service.CreateFromTransactionAsync(req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 422);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

// ── Confirm Detected Commitment ───────────────────────────────────────

internal sealed class ConfirmCommitmentEndpoint
    : EndpointWithoutRequest<CommitmentDetail>
{
    private readonly ICommitmentService _service;

    public ConfirmCommitmentEndpoint(ICommitmentService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/commitments/{commitmentId}/confirm");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Confirm a detected commitment";
            s.Description = "Transitions a detected commitment's verification status from Detected to Confirmed.";
            s.Response(200, "Commitment confirmed successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Commitment not found");
            s.Response(422, "Commitment is not in Detected status");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("commitmentId");

        try
        {
            var response = await _service.ConfirmDetectedAsync(id, ct);
            await Send.OkAsync(response, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            await Send.NotFoundAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

// ── Reject Detected Commitment ────────────────────────────────────────

internal sealed class RejectCommitmentRequest
{
    public string? Reason { get; set; }
}

internal sealed class RejectCommitmentEndpoint
    : Endpoint<RejectCommitmentRequest, object>
{
    private readonly ICommitmentService _service;

    public RejectCommitmentEndpoint(ICommitmentService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/commitments/{commitmentId}/reject");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Reject a detected commitment";
            s.Description = "Transitions a detected commitment's verification status from Detected to Rejected. Optionally accepts a reason.";
            s.Response(200, "Commitment rejected successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Commitment not found");
            s.Response(422, "Commitment is not in Detected status");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(RejectCommitmentRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("commitmentId");

        try
        {
            await _service.RejectDetectedAsync(id, req.Reason, ct);
            await Send.OkAsync(ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            await Send.NotFoundAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

// ── List Detected Commitments ─────────────────────────────────────────

internal sealed class ListDetectedCommitmentsEndpoint
    : EndpointWithoutRequest<IReadOnlyList<CommitmentItem>>
{
    private readonly ICommitmentService _service;

    public ListDetectedCommitmentsEndpoint(ICommitmentService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/commitments/detected");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List detected commitments";
            s.Description = "Returns all commitments with VerificationStatus = Detected, awaiting user review.";
            s.Response(200, "Detected commitments returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _service.ListDetectedAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
