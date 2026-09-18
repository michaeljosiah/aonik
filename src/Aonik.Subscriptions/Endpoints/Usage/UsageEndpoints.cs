using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Services.Subscriptions;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Subscriptions.Endpoints.Usage;

/// <summary>
/// What every usage endpoint does the same way: turn the meter's refusals into one problem shape.
///
/// <para>
/// Authorisation is the meter's, not the endpoint's: every call names or implies a subscriber, and
/// the meter's <c>ISubscriberAuthorizer</c> for that kind decides whether the signed-in caller may
/// act for it — a party for itself, a group's accepted members for the group (Spec 087 §5). A
/// reservation that is not the caller's to act on is not distinguishable from one that does not
/// exist, so a reservation id cannot be used to learn about another subscriber.
/// </para>
/// </summary>
internal abstract class UsageEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
{
    protected const string Tag = "Usage";

    /// <summary>The policy every customer-facing usage call requires: a signed-in user of this tenant.</summary>
    protected const string UserPolicy = "UserPolicy";

    protected Task ProblemAsync(UsageProblem problem)
        => Send.ResultAsync(Results.Json(problem, statusCode: problem.Status));

    /// <summary>Run the body; a refusal the meter makes on purpose becomes a problem response rather than a 500.</summary>
    protected async Task GuardedAsync(Func<Task> body)
    {
        try
        {
            await body();
        }
        catch (Exception ex) when (UsageProblems.From(ex) is { } problem)
        {
            await ProblemAsync(problem);
        }
    }
}

internal static class UsageProblems
{
    public static UsageProblem NoSuchReservation() => new(
        StatusCodes.Status404NotFound, "reservation-not-found", "No such reservation is available to you.");

    public static UsageProblem? From(Exception exception) => exception switch
    {
        EntitlementExceededException exceeded => new(StatusCodes.Status402PaymentRequired, "allowance-exceeded", exceeded.Message),
        // One message for "not yours" and "no such subscriber", as the authorizer intends.
        PermissionDeniedException denied => new(StatusCodes.Status403Forbidden, "forbidden", denied.Message),
        NotFoundException => NoSuchReservation(),
        InvalidStateException invalid => new(StatusCodes.Status409Conflict, "invalid-state", invalid.Message),
        ArgumentException invalid => new(StatusCodes.Status422UnprocessableEntity, "invalid-request", invalid.Message),
        _ => null,
    };
}

/// <summary>Hold part of a subscriber's allowance before paid work starts (Spec 087 §7.2).</summary>
internal sealed class ReserveUsageEndpoint : UsageEndpoint<ReserveUsageRequest, UsageReservationResponse>
{
    private readonly IUsageMeter _meter;

    public ReserveUsageEndpoint(IUsageMeter meter) => _meter = meter;

    public override void Configure()
    {
        Post("/usage/reservations");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Reserve allowance";
            s.Description = "Holds a quantity of one meter against the subscriber's open grants, before the work it pays for starts. "
                + "The same idempotency key returns the same reservation, whatever its state, so a caller who lost the "
                + "response asks again and is not charged twice. The hold lapses on its own after the hold period.";
            s.Response(200, "The reservation as it stands: newly held, or the one the same key made earlier");
            s.Response(402, "The subscriber's allowance cannot cover the quantity");
            s.Response(403, "The caller may not act for the subscriber");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(ReserveUsageRequest req, CancellationToken ct)
    {
        await GuardedAsync(async () =>
        {
            var subscriber = new SubscriberRef(req.Subscriber.Kind, req.Subscriber.Id);
            var hold = req.HoldForSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : (TimeSpan?)null;

            var reserved = await _meter.ReserveAsync(subscriber, req.MeterCode.Trim(), req.Quantity, req.IdempotencyKey.Trim(), hold, ct);

            // The reservation as it stands, whether this call made it or an earlier one with the same key did —
            // held, committed or released — rather than a 201 that would pretend a new hold was taken.
            var state = await _meter.GetReservationAsync(reserved.ReservationId, ct)
                ?? throw new InvalidStateException("The reservation could not be read back.");

            await Send.OkAsync(UsageReservationResponse.From(state), ct);
        });
    }
}

internal sealed class GetUsageReservationEndpoint : UsageEndpoint<EmptyRequest, UsageReservationResponse>
{
    private readonly IUsageMeter _meter;

    public GetUsageReservationEndpoint(IUsageMeter meter) => _meter = meter;

    public override void Configure()
    {
        Get("/usage/reservations/{reservationId:guid}");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Get a reservation";
            s.Description = "The reservation as it stands: held, committed, released or expired. A reservation the caller may not "
                + "act on answers 404 whether or not it exists.";
            s.Response(200, "Reservation returned");
            s.Response(404, "No such reservation is available to the caller");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var state = await _meter.GetReservationAsync(Route<Guid>("reservationId"), ct);

        if (state is null)
        {
            await ProblemAsync(UsageProblems.NoSuchReservation());
            return;
        }

        await Send.OkAsync(UsageReservationResponse.From(state), ct);
    }
}

/// <summary>Turn a hold into usage, for what the work actually consumed (Spec 087 §7.3).</summary>
internal sealed class CommitUsageEndpoint : UsageEndpoint<CommitUsageRequest, UsageCommitResponse>
{
    private readonly IUsageMeter _meter;

    public CommitUsageEndpoint(IUsageMeter meter) => _meter = meter;

    public override void Configure()
    {
        Post("/usage/reservations/{reservationId:guid}/commit");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Commit a reservation";
            s.Description = "Consumes the actual quantity from the reservation's grants and returns the rest of the hold. Committing "
                + "a reservation already committed is a replay: the earlier commit stands, nothing is consumed again, and the "
                + "answer says so. A released or lapsed reservation cannot be committed.";
            s.Response(200, "Usage recorded, or the earlier commit returned");
            s.Response(404, "No such reservation is available to the caller");
            s.Response(409, "The reservation is released or has lapsed, or the quantity exceeds the hold");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(CommitUsageRequest req, CancellationToken ct)
    {
        var reservationId = Route<Guid>("reservationId");

        await GuardedAsync(async () =>
        {
            var state = await _meter.GetReservationAsync(reservationId, ct);

            if (state is null)
            {
                await ProblemAsync(UsageProblems.NoSuchReservation());
                return;
            }

            if (state.Status == UsageReservationStatuses.Committed)
            {
                await Send.OkAsync(new UsageCommitResponse(reservationId, state.Status, null, null, Replayed: true), ct);
                return;
            }

            var committed = await _meter.CommitAsync(
                reservationId,
                req.ActualQuantity,
                new UsageSource(req.SourceType.Trim(), req.SourceId, req.ProviderCost, req.ProviderCostCurrency?.Trim()),
                ct);

            await Send.OkAsync(new UsageCommitResponse(
                reservationId, UsageReservationStatuses.Committed, committed.UsageRecordId, committed.QuantityCommitted, Replayed: false), ct);
        });
    }
}

/// <summary>Give a hold back: the work did not happen, or did not need it.</summary>
internal sealed class ReleaseUsageEndpoint : UsageEndpoint<EmptyRequest, UsageReleaseResponse>
{
    private readonly IUsageMeter _meter;

    public ReleaseUsageEndpoint(IUsageMeter meter) => _meter = meter;

    public override void Configure()
    {
        Post("/usage/reservations/{reservationId:guid}/release");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Release a reservation";
            s.Description = "Returns a held quantity to the subscriber's allowance. Idempotent: releasing a reservation that is "
                + "not held — already committed, released or lapsed — changes nothing and answers with its state.";
            s.Response(200, "Released, or nothing to release");
            s.Response(404, "No such reservation is available to the caller");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var reservationId = Route<Guid>("reservationId");

        await GuardedAsync(async () =>
        {
            if (await _meter.GetReservationAsync(reservationId, ct) is null)
            {
                await ProblemAsync(UsageProblems.NoSuchReservation());
                return;
            }

            await _meter.ReleaseAsync(reservationId, ct);
            var after = await _meter.GetReservationAsync(reservationId, ct);
            await Send.OkAsync(new UsageReleaseResponse(reservationId, after?.Status ?? UsageReservationStatuses.Released), ct);
        });
    }
}

/// <summary>What a subscriber's plan grants and what is left, for the product to show before it asks (Spec 087 §6).</summary>
internal sealed class GetAllowanceEndpoint : UsageEndpoint<AllowanceQuery, AllowanceResponse>
{
    private readonly IEntitlementReader _entitlements;
    private readonly SubscriberAuthorization _authorization;

    public GetAllowanceEndpoint(IEntitlementReader entitlements, SubscriberAuthorization authorization)
    {
        _entitlements = entitlements;
        _authorization = authorization;
    }

    public override void Configure()
    {
        Get("/usage/allowance");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Get a subscriber's allowance";
            s.Description = "The subscriber's active plan and, per meter, the allowance, what is consumed, what is held and what "
                + "remains. Only for a subscriber the caller may act for.";
            s.Response(200, "Allowance returned");
            s.Response(403, "The caller may not act for the subscriber");
            s.Response(404, "The subscriber has no active subscription");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(AllowanceQuery req, CancellationToken ct)
    {
        await GuardedAsync(async () =>
        {
            var subscriber = new SubscriberRef(req.SubscriberKind, req.SubscriberId);
            await _authorization.EnsureCanActForAsync(subscriber, ct);

            var snapshot = await _entitlements.GetAsync(subscriber, ct);

            if (snapshot is null)
            {
                await ProblemAsync(new UsageProblem(StatusCodes.Status404NotFound, "no-subscription", "The subscriber has no active subscription."));
                return;
            }

            await Send.OkAsync(AllowanceResponse.From(snapshot), ct);
        });
    }
}

/// <param name="SubscriberKind">One of the subscriber kinds: <c>party</c>, <c>group</c>, <c>tenant</c>.</param>
public sealed record AllowanceQuery(string SubscriberKind, Guid SubscriberId);
