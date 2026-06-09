using Aonik.Finance.Contracts.Models.Partners;
using Aonik.Finance.Contracts.Services.Partners;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Partners.Credentials;

// Request-DTO validators (architecture rule: every Endpoint<TRequest> DTO has a Validator<T>).
public class CreateCredentialBundleRequestValidator : Validator<CreateCredentialBundleRequest>
{
    public CreateCredentialBundleRequestValidator()
    {
        RuleFor(x => x.Ref).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).MaximumLength(200);
        RuleFor(x => x.ConnectorKind).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Secrets).NotNull();
    }
}

public class UpdateCredentialBundleRequestValidator : Validator<UpdateCredentialBundleRequest>
{
    public UpdateCredentialBundleRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(200);
        RuleFor(x => x.Secrets).NotNull();
    }
}

public class RotateCredentialFieldRequestValidator : Validator<RotateCredentialFieldRequest>
{
    public RotateCredentialFieldRequestValidator()
    {
        RuleFor(x => x.Field).NotEmpty();
        RuleFor(x => x.NewValue).NotEmpty();
    }
}

/// <summary>Connector kinds + their credential / config schemas — drives the schema-generated editor (Spec 042 §12).</summary>
public class GetConnectorKindsEndpoint : EndpointWithoutRequest<IReadOnlyList<ConnectorKindSchemaDto>>
{
    private readonly ICredentialBundleAdminService _service;

    public GetConnectorKindsEndpoint(ICredentialBundleAdminService service) => _service = service;

    public override void Configure()
    {
        Get("/admin/connector-kinds");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List connector kinds and their credential/config schemas";
            s.Response(200, "Connector kind schemas");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Partners"));
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _service.GetConnectorKindsAsync(ct), ct);
}

/// <summary>Lists this tenant's credential bundles (field state only — never secret values, Spec 042 §6).</summary>
public class ListCredentialBundlesEndpoint : EndpointWithoutRequest<IReadOnlyList<CredentialBundleListItem>>
{
    private readonly ICredentialBundleAdminService _service;

    public ListCredentialBundlesEndpoint(ICredentialBundleAdminService service) => _service = service;

    public override void Configure()
    {
        Get("/admin/credential-bundles");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List credential bundles";
            s.Response(200, "Credential bundles");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Partners"));
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _service.ListBundlesAsync(ct), ct);
}

public class CreateCredentialBundleEndpoint : Endpoint<CreateCredentialBundleRequest, CredentialBundleListItem>
{
    private readonly ICredentialBundleAdminService _service;

    public CreateCredentialBundleEndpoint(ICredentialBundleAdminService service) => _service = service;

    public override void Configure()
    {
        Post("/admin/credential-bundles");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Create a credential bundle";
            s.Description = "Secrets are write-only and stored encrypted; they are never returned.";
            s.Response(200, "Bundle created");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Partners"));
    }

    public override async Task HandleAsync(CreateCredentialBundleRequest req, CancellationToken ct)
    {
        try
        {
            await Send.OkAsync(await _service.CreateBundleAsync(req, ct), ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
        }
    }
}

public class UpdateCredentialBundleEndpoint : Endpoint<UpdateCredentialBundleRequest, CredentialBundleListItem>
{
    private readonly ICredentialBundleAdminService _service;

    public UpdateCredentialBundleEndpoint(ICredentialBundleAdminService service) => _service = service;

    public override void Configure()
    {
        Patch("/admin/credential-bundles/{bundleRef}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Update a credential bundle";
            s.Response(200, "Bundle updated");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Partners"));
    }

    public override async Task HandleAsync(UpdateCredentialBundleRequest req, CancellationToken ct)
    {
        var bundleRef = Route<string>("bundleRef") ?? string.Empty;
        try
        {
            await Send.OkAsync(await _service.UpdateBundleAsync(bundleRef, req, ct), ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
        }
    }
}

public class RotateCredentialFieldEndpoint : Endpoint<RotateCredentialFieldRequest, CredentialBundleListItem>
{
    private readonly ICredentialBundleAdminService _service;

    public RotateCredentialFieldEndpoint(ICredentialBundleAdminService service) => _service = service;

    public override void Configure()
    {
        Post("/admin/credential-bundles/{bundleRef}/rotate");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Rotate a credential field (e.g. webhook signing secret)";
            s.Description = "The previous value keeps verifying for a grace window (Spec 042 §11).";
            s.Response(200, "Field rotated");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Partners"));
    }

    public override async Task HandleAsync(RotateCredentialFieldRequest req, CancellationToken ct)
    {
        var bundleRef = Route<string>("bundleRef") ?? string.Empty;
        try
        {
            await Send.OkAsync(await _service.RotateFieldAsync(bundleRef, req, ct), ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
        }
    }
}

/// <summary>Idempotently lifts the legacy Flutterwave provider-singleton settings into bundles (Spec 042 §13).</summary>
public class LiftLegacyFlutterwaveEndpoint : EndpointWithoutRequest<LiftLegacyFlutterwaveResult>
{
    private readonly ICredentialBundleAdminService _service;

    public LiftLegacyFlutterwaveEndpoint(ICredentialBundleAdminService service) => _service = service;

    public override void Configure()
    {
        Post("/admin/partners/flutterwave/lift-legacy");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Lift legacy Flutterwave settings into partner-owned credential bundles";
            s.Description = "Idempotent. Seeds the Flutterwave partner + default connectors, re-encrypts the "
                + "existing keys into bundles, and backfills ConnectorId on existing payouts/transmissions.";
            s.Response(200, "Lift result");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Partners"));
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _service.LiftLegacyFlutterwaveAsync(ct), ct);
}
