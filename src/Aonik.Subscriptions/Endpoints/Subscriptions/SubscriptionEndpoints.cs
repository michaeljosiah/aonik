using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Endpoints.Usage;
using Aonik.Subscriptions.Services.Subscriptions;

using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Aonik.Subscriptions.Endpoints.Subscriptions;

// A subscriber's subscription over HTTP (Spec 087 §6, §11): start one on a plan, and read it back.
// Authorisation is the module's own — a party subscribes itself, a group is subscribed by an owner
// or manager — so this is how a parent puts their family on a plan an operator has published, in an
// environment where no storefront has sold them one. Products reserve and spend through /usage.

/// <param name="Subscriber">Who pays: the caller's party, or a group the caller owns or manages.</param>
/// <param name="PlanCode">The plan's code; its current published version is pinned.</param>
/// <param name="PaymentMandateId">A stored mandate to charge on renewal; optional for a zero-price plan.</param>
public sealed record CreateSubscriptionRequest(SubscriberModel Subscriber, string PlanCode, Guid? PaymentMandateId = null);

internal sealed class CreateSubscriptionEndpoint : UsageEndpoint<CreateSubscriptionRequest, SubscriptionDto>
{
    private readonly ISubscriptionService _subscriptions;

    public CreateSubscriptionEndpoint(ISubscriptionService subscriptions) => _subscriptions = subscriptions;

    public override void Configure()
    {
        Post("/subscriptions");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Subscribe a subscriber to a plan";
            s.Description = "Starts a subscription on the plan's current published version, for a subscriber the caller may "
                + "manage billing for: their own party, or a group they own or manage. A subscriber already holding an "
                + "active subscription is refused.";
            s.Response(201, "Subscription started");
            s.Response(403, "The caller may not manage billing for this subscriber");
            s.Response(404, "No such plan");
            s.Response(409, "The subscriber already has an active subscription, or the plan cannot be subscribed to");
        });
        Options(x => x.WithTags("Subscriptions"));
    }

    public override async Task HandleAsync(CreateSubscriptionRequest req, CancellationToken ct)
    {
        await GuardedAsync(async () =>
        {
            try
            {
                var subscription = await _subscriptions.SubscribeAsync(
                    new SubscriberRef(req.Subscriber.Kind.Trim().ToLowerInvariant(), req.Subscriber.Id), req.PlanCode.Trim(), req.PaymentMandateId, ct);
                await Send.ResultAsync(Results.Json(subscription, statusCode: StatusCodes.Status201Created));
            }
            catch (NotFoundException ex)
            {
                await ProblemAsync(new UsageProblem(StatusCodes.Status404NotFound, "plan-not-found", ex.Message));
            }
        });
    }
}

/// <param name="SubscriberKind">One of <see cref="SubscriberKinds"/>.</param>
public sealed record GetSubscriptionQuery(string SubscriberKind, Guid SubscriberId);

internal sealed class GetSubscriptionEndpoint : UsageEndpoint<GetSubscriptionQuery, SubscriptionDto>
{
    private readonly ISubscriptionService _subscriptions;
    private readonly SubscriberAuthorization _authorization;

    public GetSubscriptionEndpoint(ISubscriptionService subscriptions, SubscriberAuthorization authorization)
    {
        _subscriptions = subscriptions;
        _authorization = authorization;
    }

    public override void Configure()
    {
        Get("/subscriptions");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Read a subscriber's subscription";
            s.Description = "The subscription occupying the subscriber's active slot, for a subscriber the caller may act for; 404 when they hold none.";
            s.Response(200, "The subscription");
            s.Response(403, "The caller may not act for this subscriber");
            s.Response(404, "The subscriber has no subscription");
        });
        Options(x => x.WithTags("Subscriptions"));
    }

    public override async Task HandleAsync(GetSubscriptionQuery req, CancellationToken ct)
    {
        await GuardedAsync(async () =>
        {
            var subscriber = new SubscriberRef(req.SubscriberKind.Trim().ToLowerInvariant(), req.SubscriberId);
            await _authorization.EnsureCanActForAsync(subscriber, ct);

            var subscription = await _subscriptions.GetForSubscriberAsync(subscriber, ct);

            if (subscription is null)
            {
                await ProblemAsync(new UsageProblem(StatusCodes.Status404NotFound, "no-subscription", "The subscriber has no subscription."));
                return;
            }

            await Send.OkAsync(subscription, ct);
        });
    }
}

internal sealed class CreateSubscriptionRequestValidator : Validator<CreateSubscriptionRequest>
{
    public CreateSubscriptionRequestValidator()
    {
        RuleFor(x => x.Subscriber).NotNull();
        RuleFor(x => x.Subscriber.Kind).Must(kind => kind?.Trim().ToLowerInvariant() is SubscriberKinds.Party or SubscriberKinds.Group or SubscriberKinds.Tenant)
            .WithMessage("subscriber.kind is party, group or tenant.");
        RuleFor(x => x.Subscriber.Id).NotEmpty();
        RuleFor(x => x.PlanCode).NotEmpty().MaximumLength(64);
    }
}

internal sealed class GetSubscriptionQueryValidator : Validator<GetSubscriptionQuery>
{
    public GetSubscriptionQueryValidator()
    {
        RuleFor(x => x.SubscriberKind).Must(kind => kind?.Trim().ToLowerInvariant() is SubscriberKinds.Party or SubscriberKinds.Group or SubscriberKinds.Tenant)
            .WithMessage("subscriberKind is party, group or tenant.");
        RuleFor(x => x.SubscriberId).NotEmpty();
    }
}
