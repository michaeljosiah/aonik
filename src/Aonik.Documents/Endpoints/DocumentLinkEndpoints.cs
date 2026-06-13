using Aonik.Documents.Contracts;
using Aonik.Documents.Services;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Aonik.Documents.Endpoints;

// Document linking for Simi's Vault (Spec 046). Owner-scoped: a consumer manages
// links only on their own documents (a foreign id yields 404). Read uses
// AdminUserPolicy, writes use AdminUserWritePolicy — both include PersonalUser.

// ── List links ──────────────────────────────────────────────────────

internal sealed class ListDocumentLinksEndpoint : EndpointWithoutRequest<IReadOnlyList<DocumentLinkDto>>
{
    private readonly IDocumentLinkService _service;

    public ListDocumentLinksEndpoint(IDocumentLinkService service) => _service = service;

    public override void Configure()
    {
        Get("/documents/{id}/links");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List a document's links";
            s.Response(200, "Links returned");
            s.Response(401, "Not authenticated");
            s.Response(404, "Document not found");
        });
        Options(x => x.WithTags("Documents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _service.ListLinksAsync(id, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}

// ── Add link ────────────────────────────────────────────────────────

internal sealed class AddDocumentLinkRequestValidator : Validator<AddDocumentLinkRequest>
{
    private static readonly string[] TargetTypes = ["careEntity", "paymentLog", "commitment"];

    public AddDocumentLinkRequestValidator()
    {
        RuleFor(x => x.TargetType)
            .NotEmpty()
            .Must(t => TargetTypes.Contains(t))
            .WithMessage($"TargetType must be one of: {string.Join(", ", TargetTypes)}.");
        RuleFor(x => x.TargetId).RequiredId();
    }
}

internal sealed class AddDocumentLinkEndpoint : Endpoint<AddDocumentLinkRequest, DocumentLinkDto>
{
    private readonly IDocumentLinkService _service;

    public AddDocumentLinkEndpoint(IDocumentLinkService service) => _service = service;

    public override void Configure()
    {
        Post("/documents/{id}/links");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Link a document to a Simi target";
            s.Description = "Attaches the document to a CareEntity / PaymentLog / commitment (idempotent).";
            s.Response(200, "Link created");
            s.Response(401, "Not authenticated");
            s.Response(404, "Document not found");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Documents"));
    }

    public override async Task HandleAsync(AddDocumentLinkRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _service.AddLinkAsync(id, req.TargetType, req.TargetId, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}

// ── Remove link ─────────────────────────────────────────────────────

internal sealed class RemoveDocumentLinkEndpoint : EndpointWithoutRequest
{
    private readonly IDocumentLinkService _service;

    public RemoveDocumentLinkEndpoint(IDocumentLinkService service) => _service = service;

    public override void Configure()
    {
        Delete("/documents/{id}/links/{linkId}");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Remove a document link";
            s.Description = "Removes a link; the document itself is unaffected.";
            s.Response(204, "Link removed");
            s.Response(401, "Not authenticated");
            s.Response(404, "Document or link not found");
        });
        Options(x => x.WithTags("Documents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var linkId = Route<Guid>("linkId");
        var removed = await _service.RemoveLinkAsync(id, linkId, ct);
        if (!removed)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
