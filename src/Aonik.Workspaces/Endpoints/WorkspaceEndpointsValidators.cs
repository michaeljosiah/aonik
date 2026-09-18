using Aonik.SharedKernel.Abstractions.Workspaces;

using FastEndpoints;
using FluentValidation;

namespace Aonik.Workspaces.Endpoints;

// Structural validation of the workspace request DTOs; the merits — possession, access, replay —
// are the sync service's. Hash format is checked here so a malformed hash never reaches a query.

internal sealed class CreateWorkspaceRequestValidator : Validator<CreateWorkspaceRequest>
{
    public CreateWorkspaceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Kind).Must(kind => kind is null || WorkspaceKinds.All.Contains(kind)).WithMessage("Unknown workspace kind.");
        RuleFor(x => x.OwnerPartyId).NotEqual(Guid.Empty).When(x => x.OwnerPartyId is not null);
        RuleFor(x => x.BillingSubscriber!.Kind).NotEmpty().When(x => x.BillingSubscriber is not null);
        RuleFor(x => x.BillingSubscriber!.Id).NotEmpty().When(x => x.BillingSubscriber is not null);
    }
}

internal sealed class NegotiateRequestValidator : Validator<NegotiateRequest>
{
    public NegotiateRequestValidator()
    {
        RuleFor(x => x.ContentHashes).NotNull().Must(hashes => hashes.Count <= 10_000).WithMessage("At most 10,000 hashes per negotiation.");
        RuleForEach(x => x.ContentHashes).Matches("^[a-f0-9]{64}$").WithMessage("A content hash is a lowercase hex SHA-256.");
    }
}

internal sealed class CommitRequestValidator : Validator<CommitRequest>
{
    public CommitRequestValidator()
    {
        RuleFor(x => x.CommitId).NotEmpty().WithMessage("commitId is chosen by the client before the first attempt.");
        RuleFor(x => x.Manifest).NotNull().Must(manifest => manifest.Count <= 100_000).WithMessage("A manifest names at most 100,000 files.");
        RuleForEach(x => x.Manifest).ChildRules(entry =>
        {
            entry.RuleFor(e => e.Path).NotEmpty().MaximumLength(1024);
            entry.RuleFor(e => e.ContentHash).Matches("^[a-f0-9]{64}$").WithMessage("A content hash is a lowercase hex SHA-256.");
            entry.RuleFor(e => e.SizeBytes).GreaterThanOrEqualTo(0);
            entry.RuleFor(e => e.ContentType).MaximumLength(255);
        });
        RuleFor(x => x.Message).MaximumLength(1000);
    }
}

internal sealed class ResolveRevisionRequestValidator : Validator<ResolveRevisionRequest>
{
    public ResolveRevisionRequestValidator()
        => RuleFor(x => x.Resolution)
            .NotEmpty()
            .Must(r => r is not null && (r.Trim().ToLowerInvariant() is "accept" or "reject" or "supersede"))
            .WithMessage("resolution must be accept, reject or supersede.");
}
