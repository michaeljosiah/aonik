using Aonik.Finance.Contracts.Services.Payments;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Endpoints.Recipients;

// The recipient surface is the customer-facing façade over the payout-beneficiary party graph
// (Spec 008). Every operation is scoped to a customer party id. Writes use AdminUserWritePolicy;
// reads use AdminUserPolicy — mirroring the shipped /payments/payout-beneficiaries endpoints.

// ── Create ──────────────────────────────────────────────────────────────────────────────────────

/// <summary>POST /recipients — create/save a recipient and a payout rail for a customer.</summary>
public sealed class CreateRecipientEndpoint : Endpoint<SavePayoutBeneficiaryRequest, RecipientResponse>
{
    private readonly IRecipientService _recipientService;

    public CreateRecipientEndpoint(IRecipientService recipientService) => _recipientService = recipientService;

    public override void Configure()
    {
        Post("/recipients");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Create a recipient";
            s.Description = "Saves a recipient and a payout rail for a customer, creating the recipient party "
                + "(when not supplied), the customer→recipient relationship, and the Beneficiary role. Stores "
                + "only a masked identifier plus the connector's reusable token — never the raw account number.";
            s.Response(201, "Recipient created");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Recipients"));
    }

    public override async Task HandleAsync(SavePayoutBeneficiaryRequest req, CancellationToken ct)
    {
        var response = await _recipientService.CreateAsync(req, ct);

        await Send.CreatedAtAsync<GetRecipientEndpoint>(
            routeValues: new { recipientPartyId = response.RecipientPartyId, customerPartyId = req.CustomerPartyId },
            responseBody: response,
            cancellation: ct);
    }
}

// ── List ────────────────────────────────────────────────────────────────────────────────────────

public sealed class ListRecipientsRequest
{
    /// <summary>The owning customer whose recipients are listed (required).</summary>
    public Guid CustomerPartyId { get; set; }

    /// <summary>Optional case-insensitive display-name filter.</summary>
    public string? Search { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>GET /recipients?customerPartyId=&amp;search=&amp;page=&amp;pageSize= — list a customer's recipients.</summary>
public sealed class ListRecipientsEndpoint : Endpoint<ListRecipientsRequest, RecipientListResponse>
{
    private readonly IRecipientService _recipientService;

    public ListRecipientsEndpoint(IRecipientService recipientService) => _recipientService = recipientService;

    public override void Configure()
    {
        Get("/recipients");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List a customer's recipients";
            s.Description = "Returns the customer's payable recipients (each with identity, relationship, photo, "
                + "and saved rails), with optional name search and paging.";
            s.Response(200, "Recipients retrieved");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Recipients"));
    }

    public override async Task HandleAsync(ListRecipientsRequest req, CancellationToken ct)
    {
        var query = new RecipientQuery(
            req.Search,
            req.Page <= 0 ? 1 : req.Page,
            req.PageSize <= 0 ? 20 : req.PageSize);

        var response = await _recipientService.ListAsync(req.CustomerPartyId, query, ct);
        await Send.OkAsync(response, ct);
    }
}

// ── Get one ─────────────────────────────────────────────────────────────────────────────────────

public sealed class GetRecipientRequest
{
    public Guid RecipientPartyId { get; set; }
    public Guid CustomerPartyId { get; set; }
}

/// <summary>GET /recipients/{recipientPartyId}?customerPartyId= — read one recipient owned by the customer.</summary>
public sealed class GetRecipientEndpoint : Endpoint<GetRecipientRequest, RecipientResponse>
{
    private readonly IRecipientService _recipientService;

    public GetRecipientEndpoint(IRecipientService recipientService) => _recipientService = recipientService;

    public override void Configure()
    {
        Get("/recipients/{recipientPartyId}");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Get a recipient";
            s.Description = "Returns one recipient (identity + relationship + photo + saved rails) owned by the customer.";
            s.Response(200, "Recipient retrieved");
            s.Response(401, "Not authenticated");
            s.Response(404, "Recipient not found for this customer");
        });
        Options(x => x.WithTags("Recipients"));
    }

    public override async Task HandleAsync(GetRecipientRequest req, CancellationToken ct)
    {
        var response = await _recipientService.GetAsync(req.CustomerPartyId, req.RecipientPartyId, ct);

        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}

// ── Update ──────────────────────────────────────────────────────────────────────────────────────

public sealed class UpdateRecipientRouteRequest
{
    public Guid RecipientPartyId { get; set; }
    public Guid CustomerPartyId { get; set; }
    public string? RelationshipTypeCode { get; set; }
    public string? Notes { get; set; }
}

/// <summary>PUT /recipients/{recipientPartyId} — update the customer→recipient edge (type and/or notes).</summary>
public sealed class UpdateRecipientEndpoint : Endpoint<UpdateRecipientRouteRequest, RecipientResponse>
{
    private readonly IRecipientService _recipientService;

    public UpdateRecipientEndpoint(IRecipientService recipientService) => _recipientService = recipientService;

    public override void Configure()
    {
        Put("/recipients/{recipientPartyId}");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Update a recipient";
            s.Description = "Updates the relationship type and/or notes on the customer→recipient edge. "
                + "Rails are changed by saving (idempotent); the recipient's display name is the shared party's "
                + "and is not edited here.";
            s.Response(200, "Recipient updated");
            s.Response(401, "Not authenticated");
            s.Response(404, "Recipient not found for this customer");
        });
        Options(x => x.WithTags("Recipients"));
    }

    public override async Task HandleAsync(UpdateRecipientRouteRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _recipientService.UpdateAsync(
                req.CustomerPartyId,
                req.RecipientPartyId,
                new UpdateRecipientRequest(req.RelationshipTypeCode, req.Notes),
                ct);

            await Send.OkAsync(response, ct);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}

// ── Remove ──────────────────────────────────────────────────────────────────────────────────────

public sealed class RemoveRecipientRequest
{
    public Guid RecipientPartyId { get; set; }
    public Guid CustomerPartyId { get; set; }
}

/// <summary>DELETE /recipients/{recipientPartyId}?customerPartyId= — soft-remove a recipient.</summary>
public sealed class RemoveRecipientEndpoint : Endpoint<RemoveRecipientRequest>
{
    private readonly IRecipientService _recipientService;

    public RemoveRecipientEndpoint(IRecipientService recipientService) => _recipientService = recipientService;

    public override void Configure()
    {
        Delete("/recipients/{recipientPartyId}");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Remove a recipient";
            s.Description = "Soft-removes the recipient for this customer: soft-deletes their saved rails and "
                + "deactivates the owning Recipient edge. The party and all historical orders are preserved.";
            s.Response(204, "Recipient removed");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Recipients"));
    }

    public override async Task HandleAsync(RemoveRecipientRequest req, CancellationToken ct)
    {
        await _recipientService.RemoveAsync(req.CustomerPartyId, req.RecipientPartyId, ct);
        await Send.NoContentAsync(ct);
    }
}

// ── Photo ───────────────────────────────────────────────────────────────────────────────────────

/// <summary>POST /recipients/{recipientPartyId}/photo?customerPartyId= — upload a recipient's photo.</summary>
public sealed class UploadRecipientPhotoEndpoint : EndpointWithoutRequest<RecipientPhotoResponse>
{
    private readonly IRecipientService _recipientService;
    private readonly ILogger<UploadRecipientPhotoEndpoint> _logger;

    public UploadRecipientPhotoEndpoint(
        IRecipientService recipientService,
        ILogger<UploadRecipientPhotoEndpoint> logger)
    {
        _recipientService = recipientService;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/recipients/{recipientPartyId}/photo");
        Policies("AdminUserWritePolicy");
        AllowFileUploads();
        Summary(s =>
        {
            s.Summary = "Upload a recipient photo";
            s.Description = "Uploads a recipient's photo (original + thumbnails) onto their party profile and "
                + "returns the URLs. The recipient must be owned by the supplied customer.";
            s.Response(200, "Photo uploaded");
            s.Response(401, "Not authenticated");
            s.Response(404, "Recipient not found for this customer");
            s.Response(422, "Invalid or missing photo file");
        });
        Options(x => x.WithTags("Recipients"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var recipientPartyId = Route<Guid>("recipientPartyId");
        var customerPartyId = Query<Guid>("customerPartyId");

        if (Files.Count == 0)
        {
            HttpContext.Response.StatusCode = 422;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "A photo file is required." }, ct);
            return;
        }

        var file = Files[0];
        if (file.Length == 0)
        {
            HttpContext.Response.StatusCode = 422;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "The photo file is empty." }, ct);
            return;
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _recipientService.UploadPhotoAsync(
                customerPartyId,
                recipientPartyId,
                file.ContentType ?? "application/octet-stream",
                stream,
                ct);

            await Send.OkAsync(result, ct);
        }
        catch (ArgumentException ex)
        {
            HttpContext.Response.StatusCode = 422;
            await HttpContext.Response.WriteAsJsonAsync(new { error = ex.Message }, ct);
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await HttpContext.Response.WriteAsJsonAsync(new { error = ex.Message }, ct);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Recipient photo upload failed due to storage I/O for recipient {RecipientPartyId}", recipientPartyId);
            HttpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Unable to save photo — storage is temporarily unavailable." }, ct);
        }
    }
}
