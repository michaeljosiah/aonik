using Aonik.Platform.Contracts.Api.Consent;
using Aonik.SharedKernel.Abstractions.Consent;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Consent;

/// <summary>
/// The operator's terms (Spec 095 §10.2, Spec 096 §16): publish a version naming the processors it
/// discloses, and read the versions on record. A guardian's grant names a version; the classification
/// route a child's content may take is one that version names. So before a family can consent to
/// anything, the tenant needs a version to consent to — and this is where it is written.
/// </summary>
internal sealed class PublishConsentTermsEndpoint : Endpoint<PublishConsentTermsRequest, PublishConsentTermsResponse>
{
    private readonly IConsentService _consent;

    public PublishConsentTermsEndpoint(IConsentService consent) => _consent = consent;

    public override void Configure()
    {
        Post("/admin/consent/terms");
        Policies("AdminWritePolicy");
        Summary(s =>
        {
            s.Summary = "Publish a consent terms version";
            s.Description = "Records the version as the tenant's current terms, with the providers it names. Every active "
                + "grant under another version is revoked at publication for each affected purpose — processing stops "
                + "for those families until they re-consent, which is the point. Publishing the same version again "
                + "updates its named providers and revokes nothing that already names it.";
            s.Response(201, "Terms published");
            s.Response(422, "No version, or no provider named");
        });
        Options(x => x.WithTags("Consent"));
    }

    public override async Task HandleAsync(PublishConsentTermsRequest req, CancellationToken ct)
    {
        var revoked = await _consent.PublishTermsVersionAsync(
            new PublishTermsRequest(req.Version.Trim(), req.AffectedPurposes ?? [], req.NamedProviders), ct);

        var published = (await _consent.ListTermsVersionsAsync(ct)).First(v => v.Version == req.Version.Trim());

        await Send.CreatedAtAsync<ListConsentTermsEndpoint>(
            null,
            new PublishConsentTermsResponse(published.Version, published.NamedProviders, published.PublishedAt, published.IsCurrent, revoked),
            cancellation: ct);
    }
}

internal sealed class ListConsentTermsEndpoint : EndpointWithoutRequest<ConsentTermsVersionsResponse>
{
    private readonly IConsentService _consent;

    public ListConsentTermsEndpoint(IConsentService consent) => _consent = consent;

    public override void Configure()
    {
        Get("/admin/consent/terms");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List consent terms versions";
            s.Description = "The tenant's terms versions, newest first, with the providers each names and which is current.";
            s.Response(200, "Versions returned");
        });
        Options(x => x.WithTags("Consent"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var versions = await _consent.ListTermsVersionsAsync(ct);

        await Send.OkAsync(new ConsentTermsVersionsResponse(
            [.. versions.Select(v => new ConsentTermsVersionResponse(v.Version, v.NamedProviders, v.PublishedAt, v.IsCurrent))]), ct);
    }
}
