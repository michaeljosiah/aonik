using System.Text.RegularExpressions;
using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Payments;

// ── Spec 007 — customer card vault (tokenised payment methods) ──────────────
// Consumer-facing, owner-scoped under /payments/methods. The owning customer is resolved
// server-side from the authenticated user; routes never accept an owner id. Token-only: no
// PAN/CVV/PCI data is exchanged or stored — the setup intent hands the SDK a client secret to
// tokenise a card off-platform, and save persists only the resulting token + masked metadata.

/// <summary>POST /payments/methods/setup-intent — start a provider setup intent for vaulting a card.</summary>
public sealed class CreateSetupIntentEndpoint : EndpointWithoutRequest<SetupIntentResponse>
{
    private readonly IPaymentMethodService _service;

    public CreateSetupIntentEndpoint(IPaymentMethodService service) => _service = service;

    public override void Configure()
    {
        Post("/payments/methods/setup-intent");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Start a card-vault setup intent";
            s.Description = "Returns a provider client secret and the accepted payment method types. "
                + "The frontend SDK uses the secret to collect and tokenise a card directly with the provider.";
            s.Response(200, "Setup intent created");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _service.CreateSetupIntentAsync(ct);
        await Send.OkAsync(result, ct);
    }
}

/// <summary>GET /payments/methods — list the current customer's saved methods (masked).</summary>
public sealed class ListPaymentMethodsEndpoint : EndpointWithoutRequest<IReadOnlyList<PaymentMethodResponse>>
{
    private readonly IPaymentMethodService _service;

    public ListPaymentMethodsEndpoint(IPaymentMethodService service) => _service = service;

    public override void Configure()
    {
        Get("/payments/methods");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List saved payment methods";
            s.Description = "Returns the current customer's vaulted methods with masked display fields (brand, last four, expiry).";
            s.Response(200, "Payment methods retrieved");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _service.ListAsync(ct);
        await Send.OkAsync(result, ct);
    }
}

/// <summary>GET /payments/methods/active — saved methods whose provider is still an available gateway.</summary>
public sealed class ListActivePaymentMethodsEndpoint : EndpointWithoutRequest<IReadOnlyList<PaymentMethodResponse>>
{
    private readonly IPaymentMethodService _service;

    public ListActivePaymentMethodsEndpoint(IPaymentMethodService service) => _service = service;

    public override void Configure()
    {
        Get("/payments/methods/active");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List active payment methods";
            s.Description = "Returns only methods whose vaulting provider is currently an available gateway.";
            s.Response(200, "Active payment methods retrieved");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _service.ListActiveAsync(ct);
        await Send.OkAsync(result, ct);
    }
}

/// <summary>GET /payments/methods/{id} — a single owned method.</summary>
public sealed class GetPaymentMethodEndpoint : EndpointWithoutRequest<PaymentMethodResponse>
{
    private readonly IPaymentMethodService _service;

    public GetPaymentMethodEndpoint(IPaymentMethodService service) => _service = service;

    public override void Configure()
    {
        Get("/payments/methods/{id:guid}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get a saved payment method";
            s.Description = "Returns a single vaulted method owned by the current customer.";
            s.Response(200, "Payment method retrieved");
            s.Response(401, "Not authenticated");
            s.Response(404, "Payment method not found");
        });
        Options(x => x.WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _service.GetAsync(id, ct);

        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}

/// <summary>POST /payments/methods — vault an already-tokenised instrument for the current customer.</summary>
public sealed class SavePaymentMethodEndpoint : Endpoint<SavePaymentMethodRequest, PaymentMethodResponse>
{
    private readonly IPaymentMethodService _service;

    public SavePaymentMethodEndpoint(IPaymentMethodService service) => _service = service;

    public override void Configure()
    {
        Post("/payments/methods");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Save a payment method";
            s.Description = "Links an already-tokenised instrument (gateway token + masked metadata) to the current customer. "
                + "Idempotent on (provider, token). Never accepts a raw PAN.";
            s.Response(200, "Payment method saved");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Payments"));
    }

    public override async Task HandleAsync(SavePaymentMethodRequest req, CancellationToken ct)
    {
        var result = await _service.SaveAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}

/// <summary>DELETE /payments/methods/{id} — remove an owned method.</summary>
public sealed class DeletePaymentMethodEndpoint : EndpointWithoutRequest
{
    private readonly IPaymentMethodService _service;

    public DeletePaymentMethodEndpoint(IPaymentMethodService service) => _service = service;

    public override void Configure()
    {
        Delete("/payments/methods/{id:guid}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Remove a payment method";
            s.Description = "Deletes a vaulted method owned by the current customer.";
            s.Response(204, "Payment method removed");
            s.Response(401, "Not authenticated");
            s.Response(404, "Payment method not found");
        });
        Options(x => x.WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var removed = await _service.DeleteAsync(id, ct);

        if (!removed)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

/// <summary>Validates a save request — fail closed on anything that looks like raw card data.</summary>
public sealed class SavePaymentMethodRequestValidator : Validator<SavePaymentMethodRequest>
{
    public SavePaymentMethodRequestValidator()
    {
        RuleFor(x => x.ProviderToken)
            .NotEmpty().WithMessage("A provider token is required.")
            .MaximumLength(200)
            .Must(NotLookLikeRawPan)
            .WithMessage("A raw card number must not be sent; provide a gateway vault token.");

        // No PCI data in any persisted free-form field — mirror the token guard so a PAN can't be
        // smuggled through display metadata. Defence in depth with the service-layer RejectRawPan.
        const string rawPan = "A raw card number must not be sent; provide tokenised data only.";
        RuleFor(x => x.Provider).MaximumLength(50).Must(NotLookLikeRawPan).WithMessage(rawPan);
        RuleFor(x => x.Type).MaximumLength(30).Must(NotLookLikeRawPan).WithMessage(rawPan);
        RuleFor(x => x.Brand).MaximumLength(30).Must(NotLookLikeRawPan).WithMessage(rawPan);
        RuleFor(x => x.Label).MaximumLength(100).Must(NotLookLikeRawPan).WithMessage(rawPan);
        RuleFor(x => x.ProviderCustomerRef).MaximumLength(200).Must(NotLookLikeRawPan).WithMessage(rawPan);

        RuleFor(x => x.Last4!)
            .Matches("^[0-9]{4}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Last4))
            .WithMessage("Last4 must be exactly four digits.");

        RuleFor(x => x.ExpiryMonth!.Value)
            .InclusiveBetween(1, 12)
            .When(x => x.ExpiryMonth.HasValue)
            .WithMessage("Expiry month must be between 1 and 12.");

        RuleFor(x => x.ExpiryYear!.Value)
            .InclusiveBetween(2000, 2100)
            .When(x => x.ExpiryYear.HasValue)
            .WithMessage("Expiry year is out of range.");
    }

    // A run of 13–19 digits (optionally grouped by single spaces or hyphens) anywhere in the value —
    // standalone OR embedded in surrounding text — is treated as a raw PAN.
    private static readonly Regex PanLikePattern =
        new(@"[0-9](?:[ -]?[0-9]){12,18}", RegexOptions.Compiled);

    private static bool NotLookLikeRawPan(string? value)
        => string.IsNullOrWhiteSpace(value) || !PanLikePattern.IsMatch(value);
}
