using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.PersonalFinance;

// CareEntity customer CRUD + profile (Spec 043). Customer-scoped
// (UserPolicy), under /personal-finance/care-entities. Internal route-binding
// request classes and their validators are co-located here (same assembly →
// visibility OK); the public record DTO validators live in
// PersonalFinanceValidators.cs alongside CreateBillRequestValidator.

// ── List CareEntities ───────────────────────────────────────────────

internal sealed class ListCareEntitiesRequest
{
    public string? Kind { get; set; }
    public string? AssetType { get; set; }
    public bool IncludeArchived { get; set; }
}

internal sealed class ListCareEntitiesRequestValidator : Validator<ListCareEntitiesRequest>
{
    public ListCareEntitiesRequestValidator()
    {
        RuleFor(x => x.Kind).MaximumLength(16);
        RuleFor(x => x.AssetType).MaximumLength(32);
    }
}

internal sealed class ListCareEntitiesEndpoint : Endpoint<ListCareEntitiesRequest, IReadOnlyList<CareEntityResponse>>
{
    private readonly ICareEntityService _service;

    public ListCareEntitiesEndpoint(ICareEntityService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/care-entities");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List care entities";
            s.Description = "Returns the authenticated user's people and assets, filterable by kind, assetType, and archived state.";
            s.Response(200, "Care entities returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(ListCareEntitiesRequest req, CancellationToken ct)
    {
        var response = await _service.ListAsync(req.Kind, req.AssetType, req.IncludeArchived, ct);
        await Send.OkAsync(response, ct);
    }
}

// ── Get CareEntity ──────────────────────────────────────────────────

internal sealed class GetCareEntityRequest
{
    public Guid Id { get; set; }
}

internal sealed class GetCareEntityRequestValidator : Validator<GetCareEntityRequest>
{
    public GetCareEntityRequestValidator() => RuleFor(x => x.Id).RequiredId();
}

internal sealed class GetCareEntityEndpoint : Endpoint<GetCareEntityRequest, CareEntityResponse>
{
    private readonly ICareEntityService _service;

    public GetCareEntityEndpoint(ICareEntityService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/care-entities/{Id}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get a care entity";
            s.Description = "Returns a single care entity owned by the authenticated user.";
            s.Response(200, "Care entity returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Care entity not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(GetCareEntityRequest req, CancellationToken ct)
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

// ── Create CareEntity ───────────────────────────────────────────────

internal sealed class CreateCareEntityEndpoint : Endpoint<CreateCareEntityRequest, CareEntityResponse>
{
    private readonly ICareEntityService _service;

    public CreateCareEntityEndpoint(ICareEntityService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/care-entities");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Create a care entity";
            s.Description = "Creates a person or asset for the authenticated user.";
            s.Response(201, "Care entity created successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CreateCareEntityRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _service.CreateAsync(req, ct);
            await Send.CreatedAtAsync<GetCareEntityEndpoint>(
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

// ── Update CareEntity ───────────────────────────────────────────────

internal sealed class UpdateCareEntityRouteRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AssetType { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string? Relationship { get; set; }
    public string? Emoji { get; set; }
    public Guid? PhotoDocumentId { get; set; }
    public IReadOnlyDictionary<string, string>? Attributes { get; set; }
}

internal sealed class UpdateCareEntityRouteRequestValidator : Validator<UpdateCareEntityRouteRequest>
{
    public UpdateCareEntityRouteRequestValidator()
    {
        RuleFor(x => x.Id).RequiredId();
        RuleFor(x => x.Name).RequiredText(120);
        RuleFor(x => x.CountryCode).CountryCode();
        RuleFor(x => x.AssetType).MaximumLength(32);
        RuleFor(x => x.Relationship).MaximumLength(80);
        RuleFor(x => x.Emoji).MaximumLength(16);
        RuleFor(x => x.PhotoDocumentId).ValidIdWhenSupplied();
    }
}

internal sealed class UpdateCareEntityEndpoint : Endpoint<UpdateCareEntityRouteRequest, CareEntityResponse>
{
    private readonly ICareEntityService _service;

    public UpdateCareEntityEndpoint(ICareEntityService service) => _service = service;

    public override void Configure()
    {
        Put("/personal-finance/care-entities/{Id}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Update a care entity";
            s.Description = "Updates name, relationship, country, asset type, attributes, and photo of a care entity. Kind is immutable.";
            s.Response(200, "Care entity updated successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Care entity not found");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(UpdateCareEntityRouteRequest req, CancellationToken ct)
    {
        var updateRequest = new UpdateCareEntityRequest(
            req.Name,
            req.AssetType,
            req.CountryCode,
            req.Relationship,
            req.Emoji,
            req.PhotoDocumentId,
            req.Attributes);

        try
        {
            var response = await _service.UpdateAsync(req.Id, updateRequest, ct);
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

// ── Archive CareEntity ──────────────────────────────────────────────

internal sealed class ArchiveCareEntityRequest
{
    public Guid Id { get; set; }
}

internal sealed class ArchiveCareEntityRequestValidator : Validator<ArchiveCareEntityRequest>
{
    public ArchiveCareEntityRequestValidator() => RuleFor(x => x.Id).RequiredId();
}

internal sealed class ArchiveCareEntityEndpoint : Endpoint<ArchiveCareEntityRequest>
{
    private readonly ICareEntityService _service;

    public ArchiveCareEntityEndpoint(ICareEntityService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/care-entities/{Id}/archive");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Archive a care entity";
            s.Description = "Archives a care entity (soft; history preserved, never hard-deleted).";
            s.Response(204, "Care entity archived successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Care entity not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(ArchiveCareEntityRequest req, CancellationToken ct)
    {
        var archived = await _service.ArchiveAsync(req.Id, ct);
        if (!archived)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

// ── CareEntity Profile (§8) ─────────────────────────────────────────

internal sealed class CareEntityProfileRequest
{
    public Guid Id { get; set; }
}

internal sealed class CareEntityProfileRequestValidator : Validator<CareEntityProfileRequest>
{
    public CareEntityProfileRequestValidator() => RuleFor(x => x.Id).RequiredId();
}

internal sealed class GetCareEntityProfileEndpoint : Endpoint<CareEntityProfileRequest, CareEntityProfileResponse>
{
    private readonly ICareEntityProfileService _service;

    public GetCareEntityProfileEndpoint(ICareEntityProfileService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/care-entities/{Id}/profile");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get a care entity profile";
            s.Description = "One-call profile projection: entity + per-currency totals + commitments + recent logs + document refs. Dependent arrays fill in as Specs 044-046 land.";
            s.Response(200, "Profile returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Care entity not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CareEntityProfileRequest req, CancellationToken ct)
    {
        var response = await _service.GetProfileAsync(req.Id, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}

// ── Banner photo upload / remove (Spec 049 Part B) ──────────────────

internal sealed class SetCareEntityPhotoEndpoint : EndpointWithoutRequest<CareEntityResponse>
{
    private readonly ICareEntityPhotoService _service;

    public SetCareEntityPhotoEndpoint(ICareEntityPhotoService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/care-entities/{Id}/photo");
        Policies("UserPolicy");
        AllowFileUploads();
        Summary(s =>
        {
            s.Summary = "Set a care entity's banner photo";
            s.Description = "Uploads (or replaces) the entity's single banner image via multipart form. Returns the updated entity with a resolved, signed photoUrl.";
            s.Response(200, "Photo set successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Care entity not found");
            s.Response(422, "Invalid image (content type or size)");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("Id");

        if (Files.Count == 0)
        {
            ThrowError("An image file is required.", 422);
            return;
        }

        var file = Files[0];
        if (file.Length == 0)
        {
            ThrowError("The image file is empty.", 422);
            return;
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var response = await _service.SetPhotoAsync(
                id, stream, file.FileName, file.ContentType, file.Length, ct);

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

internal sealed class RemoveCareEntityPhotoRequest
{
    public Guid Id { get; set; }
}

internal sealed class RemoveCareEntityPhotoRequestValidator : Validator<RemoveCareEntityPhotoRequest>
{
    public RemoveCareEntityPhotoRequestValidator() => RuleFor(x => x.Id).RequiredId();
}

internal sealed class RemoveCareEntityPhotoEndpoint : Endpoint<RemoveCareEntityPhotoRequest>
{
    private readonly ICareEntityPhotoService _service;

    public RemoveCareEntityPhotoEndpoint(ICareEntityPhotoService service) => _service = service;

    public override void Configure()
    {
        Delete("/personal-finance/care-entities/{Id}/photo");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Remove a care entity's banner photo";
            s.Description = "Clears the entity's banner image and erases the underlying document.";
            s.Response(204, "Photo removed successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Care entity not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(RemoveCareEntityPhotoRequest req, CancellationToken ct)
    {
        var removed = await _service.RemovePhotoAsync(req.Id, ct);
        if (!removed)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
