using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.PersonalFinance;

// PaymentLog customer CRUD + corroboration + year summary (Spec 045).
// Customer-scoped (UserPolicy), under /personal-finance/payment-logs. Internal
// route-binding request classes and their validators are co-located here; the
// public CreatePaymentLogRequest validator lives in PersonalFinanceValidators.cs.

internal static class PaymentLogChannels
{
    internal static readonly string[] Channels = ["bank", "wise", "cash", "other"];
    internal static readonly string[] Origins =
        ["manual", "captureImage", "captureText", "captureVoice", "markDone", "plaidDetected"];
}

// ── Create ──────────────────────────────────────────────────────────

internal sealed class CreatePaymentLogEndpoint : Endpoint<CreatePaymentLogRequest, PaymentLogResponse>
{
    private readonly IPaymentLogService _service;

    public CreatePaymentLogEndpoint(IPaymentLogService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/payment-logs");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Create a payment log";
            s.Description = "Records one act of support (idempotent on IdempotencyKey), standalone or honouring a commitment cycle.";
            s.Response(201, "Payment log created successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CreatePaymentLogRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _service.CreateAsync(req, ct);
            await Send.CreatedAtAsync<GetPaymentLogEndpoint>(
                routeValues: new { Id = response.Id },
                responseBody: response,
                cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

// ── List ────────────────────────────────────────────────────────────

internal sealed class ListPaymentLogsRequest
{
    public Guid? CareEntityId { get; set; }
    public Guid? CommitmentId { get; set; }
    public int? Year { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

internal sealed class ListPaymentLogsRequestValidator : Validator<ListPaymentLogsRequest>
{
    public ListPaymentLogsRequestValidator()
    {
        RuleFor(x => x.CareEntityId).ValidIdWhenSupplied();
        RuleFor(x => x.CommitmentId).ValidIdWhenSupplied();
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).When(x => x.Year.HasValue);
        RuleFor(x => x.Page).PageNumber();
        RuleFor(x => x.PageSize).PageSize(1, 100);
    }
}

internal sealed class ListPaymentLogsEndpoint : Endpoint<ListPaymentLogsRequest, PaymentLogListResponse>
{
    private readonly IPaymentLogService _service;

    public ListPaymentLogsEndpoint(IPaymentLogService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/payment-logs");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List payment logs";
            s.Description = "Lists the user's payment logs, filterable by careEntityId, commitmentId, and year (paged).";
            s.Response(200, "Payment logs returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(ListPaymentLogsRequest req, CancellationToken ct)
    {
        var response = await _service.ListAsync(req.CareEntityId, req.CommitmentId, req.Year, req.Page, req.PageSize, ct);
        await Send.OkAsync(response, ct);
    }
}

// ── Get ─────────────────────────────────────────────────────────────

internal sealed class GetPaymentLogRequest
{
    public Guid Id { get; set; }
}

internal sealed class GetPaymentLogRequestValidator : Validator<GetPaymentLogRequest>
{
    public GetPaymentLogRequestValidator() => RuleFor(x => x.Id).RequiredId();
}

internal sealed class GetPaymentLogEndpoint : Endpoint<GetPaymentLogRequest, PaymentLogResponse>
{
    private readonly IPaymentLogService _service;

    public GetPaymentLogEndpoint(IPaymentLogService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/payment-logs/{Id}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get a payment log";
            s.Response(200, "Payment log returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Payment log not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(GetPaymentLogRequest req, CancellationToken ct)
    {
        var response = await _service.GetAsync(req.Id, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}

// ── Update ──────────────────────────────────────────────────────────

internal sealed class UpdatePaymentLogRouteRequest
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal? ApproxGbp { get; set; }
    public DateTime Date { get; set; }
    public string Channel { get; set; } = "bank";
    public string? Note { get; set; }
}

internal sealed class UpdatePaymentLogRouteRequestValidator : Validator<UpdatePaymentLogRouteRequest>
{
    public UpdatePaymentLogRouteRequestValidator()
    {
        RuleFor(x => x.Id).RequiredId();
        RuleFor(x => x.Amount).PositiveMoney();
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.ApproxGbp).NonNegativeMoney();
        RuleFor(x => x.Channel)
            .NotEmpty()
            .Must(c => PaymentLogChannels.Channels.Contains(c))
            .WithMessage($"Channel must be one of: {string.Join(", ", PaymentLogChannels.Channels)}.");
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

internal sealed class UpdatePaymentLogEndpoint : Endpoint<UpdatePaymentLogRouteRequest, PaymentLogResponse>
{
    private readonly IPaymentLogService _service;

    public UpdatePaymentLogEndpoint(IPaymentLogService service) => _service = service;

    public override void Configure()
    {
        Put("/personal-finance/payment-logs/{Id}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Update a payment log";
            s.Response(200, "Payment log updated successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Payment log not found");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(UpdatePaymentLogRouteRequest req, CancellationToken ct)
    {
        var update = new UpdatePaymentLogRequest(req.Amount, req.Currency, req.ApproxGbp, req.Date, req.Channel, req.Note);

        try
        {
            var response = await _service.UpdateAsync(req.Id, update, ct);
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

// ── Delete (soft) ───────────────────────────────────────────────────

internal sealed class DeletePaymentLogRequest
{
    public Guid Id { get; set; }
}

internal sealed class DeletePaymentLogRequestValidator : Validator<DeletePaymentLogRequest>
{
    public DeletePaymentLogRequestValidator() => RuleFor(x => x.Id).RequiredId();
}

internal sealed class DeletePaymentLogEndpoint : Endpoint<DeletePaymentLogRequest>
{
    private readonly IPaymentLogService _service;

    public DeletePaymentLogEndpoint(IPaymentLogService service) => _service = service;

    public override void Configure()
    {
        Delete("/personal-finance/payment-logs/{Id}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Soft-delete a payment log";
            s.Description = "Soft-deletes a log (30-day restore window). History is never hard-deleted.";
            s.Response(204, "Payment log soft-deleted");
            s.Response(401, "Not authenticated");
            s.Response(404, "Payment log not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(DeletePaymentLogRequest req, CancellationToken ct)
    {
        var deleted = await _service.SoftDeleteAsync(req.Id, ct);
        if (!deleted)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

// ── Restore ─────────────────────────────────────────────────────────

internal sealed class RestorePaymentLogRequest
{
    public Guid Id { get; set; }
}

internal sealed class RestorePaymentLogRequestValidator : Validator<RestorePaymentLogRequest>
{
    public RestorePaymentLogRequestValidator() => RuleFor(x => x.Id).RequiredId();
}

internal sealed class RestorePaymentLogEndpoint : Endpoint<RestorePaymentLogRequest, PaymentLogResponse>
{
    private readonly IPaymentLogService _service;

    public RestorePaymentLogEndpoint(IPaymentLogService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/payment-logs/{Id}/restore");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Restore a soft-deleted payment log";
            s.Description = "Restores a soft-deleted log within the 30-day window.";
            s.Response(200, "Payment log restored");
            s.Response(401, "Not authenticated");
            s.Response(404, "Payment log not found or outside restore window");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(RestorePaymentLogRequest req, CancellationToken ct)
    {
        var response = await _service.RestoreAsync(req.Id, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}

// ── Corroboration link / unlink ─────────────────────────────────────

internal sealed class LinkTransactionRouteRequest
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
}

internal sealed class LinkTransactionRouteRequestValidator : Validator<LinkTransactionRouteRequest>
{
    public LinkTransactionRouteRequestValidator()
    {
        RuleFor(x => x.Id).RequiredId();
        RuleFor(x => x.TransactionId).RequiredId();
    }
}

internal sealed class LinkTransactionEndpoint : Endpoint<LinkTransactionRouteRequest, PaymentLogResponse>
{
    private readonly IPaymentLogService _service;

    public LinkTransactionEndpoint(IPaymentLogService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/payment-logs/{Id}/transaction-link");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Confirm a bank-transaction corroboration link";
            s.Description = "Links a PersonalTransaction as corroboration (CorroborationStatus -> confirmed).";
            s.Response(200, "Link confirmed");
            s.Response(401, "Not authenticated");
            s.Response(404, "Payment log not found");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(LinkTransactionRouteRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _service.LinkTransactionAsync(req.Id, req.TransactionId, ct);
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

internal sealed class UnlinkTransactionRequest
{
    public Guid Id { get; set; }
}

internal sealed class UnlinkTransactionRequestValidator : Validator<UnlinkTransactionRequest>
{
    public UnlinkTransactionRequestValidator() => RuleFor(x => x.Id).RequiredId();
}

internal sealed class UnlinkTransactionEndpoint : Endpoint<UnlinkTransactionRequest, PaymentLogResponse>
{
    private readonly IPaymentLogService _service;

    public UnlinkTransactionEndpoint(IPaymentLogService service) => _service = service;

    public override void Configure()
    {
        Delete("/personal-finance/payment-logs/{Id}/transaction-link");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Remove a corroboration link";
            s.Description = "Clears the corroboration link (CorroborationStatus -> none).";
            s.Response(200, "Link removed");
            s.Response(401, "Not authenticated");
            s.Response(404, "Payment log not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(UnlinkTransactionRequest req, CancellationToken ct)
    {
        var response = await _service.UnlinkTransactionAsync(req.Id, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}

// ── Year summary (Today hero) ───────────────────────────────────────

internal sealed class YearSummaryRequest
{
    public int Year { get; set; }
}

internal sealed class YearSummaryRequestValidator : Validator<YearSummaryRequest>
{
    public YearSummaryRequestValidator()
        => RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
}

internal sealed class YearSummaryEndpoint : Endpoint<YearSummaryRequest, YearSummary>
{
    private readonly IPaymentLogSummaryService _service;

    public YearSummaryEndpoint(IPaymentLogSummaryService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/summary/year");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Per-currency year summary";
            s.Description = "Per-currency totals across the user's payment logs for the Today hero — never a converted grand total.";
            s.Response(200, "Year summary returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(YearSummaryRequest req, CancellationToken ct)
    {
        var response = await _service.GetYearSummaryAsync(req.Year, ct);
        await Send.OkAsync(response, ct);
    }
}
