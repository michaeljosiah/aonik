using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

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
        catch (NotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (InvalidStateException ex)
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
        catch (NotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (InvalidStateException ex)
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

// ── Create Support Commitment (Spec 044) ──────────────────────────────

internal sealed class CreateSupportCommitmentEndpoint
    : Endpoint<CreateSupportCommitmentRequest, CommitmentDetail>
{
    private readonly ICommitmentService _service;

    public CreateSupportCommitmentEndpoint(ICommitmentService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/commitments");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Author a Support commitment";
            s.Description = "Creates a user-authored Support commitment attached to a CareEntity with a structured rhythm — the first manual-create path for a commitment-projected entity. Opens the first cycle and arms a reminder.";
            s.Response(201, "Commitment created successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CreateSupportCommitmentRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _service.CreateSupportAsync(req, ct);
            await Send.CreatedAtAsync<GetCommitmentEndpoint>(
                routeValues: new { commitmentId = response.CommitmentId },
                responseBody: response,
                cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

// ── Update Support Commitment ─────────────────────────────────────────

internal sealed class UpdateSupportCommitmentEndpoint
    : Endpoint<UpdateSupportCommitmentRequest, CommitmentDetail>
{
    private readonly ICommitmentService _service;

    public UpdateSupportCommitmentEndpoint(ICommitmentService service) => _service = service;

    public override void Configure()
    {
        Put("/personal-finance/commitments/{commitmentId}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Update a commitment";
            s.Description = "Edits a commitment's name, amount, rhythm, reminder lead, and notes. Never rewrites past cycles.";
            s.Response(200, "Commitment updated successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Commitment not found");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(UpdateSupportCommitmentRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("commitmentId");
        try
        {
            var response = await _service.UpdateSupportAsync(id, req, ct);
            if (response is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

// ── Mark cycle done ───────────────────────────────────────────────────

internal sealed class MarkCommitmentDoneEndpoint
    : Endpoint<MarkCommitmentDoneRequest, CommitmentDetail>
{
    private readonly ICommitmentService _service;

    public MarkCommitmentDoneEndpoint(ICommitmentService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/commitments/{commitmentId}/done");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Mark the current cycle done";
            s.Description = "Records a PaymentLog for the current cycle, rolls the due date forward, opens the next cycle, and re-arms the reminder. Idempotent per cycle.";
            s.Response(200, "Cycle marked done successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Commitment not found");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(MarkCommitmentDoneRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("commitmentId");
        try
        {
            var response = await _service.MarkDoneAsync(id, req, ct);
            if (response is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

// ── Skip cycle ────────────────────────────────────────────────────────

internal sealed class SkipCommitmentEndpoint
    : Endpoint<SkipCommitmentRequest, CommitmentDetail>
{
    private readonly ICommitmentService _service;

    public SkipCommitmentEndpoint(ICommitmentService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/commitments/{commitmentId}/skip");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Skip the current cycle";
            s.Description = "Records the current cycle as Skipped (honest history) and advances to the next cycle.";
            s.Response(200, "Cycle skipped successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Commitment not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(SkipCommitmentRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("commitmentId");
        var response = await _service.SkipCycleAsync(id, req.Reason, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}

// ── Snooze ────────────────────────────────────────────────────────────

internal sealed class SnoozeCommitmentEndpoint
    : Endpoint<SnoozeCommitmentRequest, CommitmentDetail>
{
    private readonly ICommitmentService _service;

    public SnoozeCommitmentEndpoint(ICommitmentService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/commitments/{commitmentId}/snooze");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Snooze the current cycle's reminder";
            s.Description = "Reschedules the current cycle's reminder to a chosen date without resolving the cycle.";
            s.Response(200, "Reminder snoozed successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Commitment not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(SnoozeCommitmentRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("commitmentId");
        var response = await _service.SnoozeAsync(id, req.Until, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}

// ── Pause / Resume ────────────────────────────────────────────────────

internal sealed class PauseCommitmentEndpoint : EndpointWithoutRequest<CommitmentDetail>
{
    private readonly ICommitmentService _service;

    public PauseCommitmentEndpoint(ICommitmentService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/commitments/{commitmentId}/pause");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Pause a commitment";
            s.Description = "Pauses reminders for a commitment until resumed.";
            s.Response(200, "Commitment paused successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Commitment not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("commitmentId");
        var response = await _service.PauseAsync(id, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}

internal sealed class ResumeCommitmentEndpoint : EndpointWithoutRequest<CommitmentDetail>
{
    private readonly ICommitmentService _service;

    public ResumeCommitmentEndpoint(ICommitmentService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/commitments/{commitmentId}/resume");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Resume a commitment";
            s.Description = "Resumes a paused commitment and re-arms its reminder.";
            s.Response(200, "Commitment resumed successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Commitment not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("commitmentId");
        var response = await _service.ResumeAsync(id, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}

// ── Cycle history ─────────────────────────────────────────────────────

internal sealed class ListCommitmentCyclesEndpoint : EndpointWithoutRequest<IReadOnlyList<CommitmentCycleResponse>>
{
    private readonly ICommitmentService _service;

    public ListCommitmentCyclesEndpoint(ICommitmentService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/commitments/{commitmentId}/cycles");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List commitment cycles";
            s.Description = "Per-cycle history timeline (paid / skipped / snoozed), newest first, paged.";
            s.Response(200, "Cycles returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Commitment not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("commitmentId");
        var page = Query<int?>("page", isRequired: false) ?? 1;
        var pageSize = Query<int?>("pageSize", isRequired: false) ?? 20;

        var response = await _service.GetCyclesAsync(id, page, pageSize, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}
