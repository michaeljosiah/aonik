using System.Text.Json;
using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Orders;
using Aonik.Application.Services.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Domain.Orders.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Orders;

public class PublicOrderService : IPublicOrderService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;

    public PublicOrderService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        IAuditLogWriter auditLogWriter,
        IClock clock,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _auditLogWriter = auditLogWriter;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<GuestBillPaymentDraftResponse> CreateGuestBillPaymentDraftAsync(
        CreateGuestBillPaymentDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var countryCode = request.CountryCode.Trim().ToUpperInvariant();
        var currency = request.Currency.Trim().ToUpperInvariant();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderType = "BillPayment",
            Status = "Draft",
            OriginCountry = countryCode,
            CurrencyIn = currency,
            AmountIn = request.RequestedAmount ?? 0m,
            FeesJson = "[]",
            ProvenanceJson = JsonSerializer.Serialize(new
            {
                source = "GuestDraft",
                channel = string.IsNullOrWhiteSpace(request.Channel) ? "Payabo" : request.Channel.Trim(),
                capturedAt = request.CapturedAt,
                countryCode,
                intent = new
                {
                    request.BillerId,
                    request.BillerName,
                    request.ServiceId,
                    request.ServiceCode,
                    request.ServiceName,
                    serviceFieldValues = request.ServiceFieldValues,
                    request.IsValidated,
                    request.ValidationMode,
                    request.AccountHolderName,
                    request.RequestedAmount
                }
            }, JsonOptions),
            CreatedAt = now,
            CreatedBy = _currentUserProvider.GetCurrentUserId()
        };

        order.HistoryEvents.Add(new OrderHistoryEvent
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            EventType = "GuestDraftCaptured",
            EventAt = now,
            ActorType = "System",
            ActorId = Guid.Empty,
            DetailsJson = JsonSerializer.Serialize(new
            {
                request.BillerId,
                request.ServiceId,
                request.IsValidated
            }, JsonOptions),
            TenantId = tenantId,
            CreatedAt = now,
            CreatedBy = _currentUserProvider.GetCurrentUserId()
        });

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.OrderCreated,
            "Order",
            order.Id,
            tenantId,
            _currentUserProvider.GetCurrentUserId(),
            correlationId: null,
            detailsJson: JsonSerializer.Serialize(new { OrderId = order.Id, Source = "GuestDraft", order.Status }, JsonOptions),
            cancellationToken: cancellationToken);

        return new GuestBillPaymentDraftResponse(order.Id, order.Status, order.CreatedAt);
    }

    public async Task<GuestBillPaymentDraftDetailResponse?> GetGuestBillPaymentDraftAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == orderId, cancellationToken);

        if (order == null || !string.Equals(order.OrderType, "BillPayment", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var draft = DeserializeGuestDraft(order.ProvenanceJson);
        if (draft == null)
        {
            return null;
        }

        return new GuestBillPaymentDraftDetailResponse(
            order.Id,
            order.Status,
            order.CreatedAt,
            order.OriginCountry ?? draft.CountryCode,
            order.CurrencyIn,
            draft.BillerId,
            draft.BillerName,
            draft.ServiceId,
            draft.ServiceCode,
            draft.ServiceName,
            draft.ServiceFieldValues,
            draft.IsValidated,
            draft.CapturedAt,
            draft.ValidationMode,
            draft.AccountHolderName,
            draft.RequestedAmount,
            string.IsNullOrWhiteSpace(draft.Channel) ? "Payabo" : draft.Channel);
    }

    private static GuestDraftIntent? DeserializeGuestDraft(string provenanceJson)
    {
        if (string.IsNullOrWhiteSpace(provenanceJson))
        {
            return null;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<GuestDraftEnvelope>(provenanceJson, JsonOptions);
            if (envelope == null || !string.Equals(envelope.Source, "GuestDraft", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (envelope.Intent == null)
            {
                return null;
            }

            return new GuestDraftIntent(
                envelope.Intent.BillerId,
                envelope.Intent.BillerName,
                envelope.Intent.ServiceId,
                envelope.Intent.ServiceCode,
                envelope.Intent.ServiceName,
                envelope.Intent.ServiceFieldValues ?? new Dictionary<string, string>(),
                envelope.Intent.IsValidated,
                envelope.CapturedAt,
                envelope.Intent.ValidationMode,
                envelope.Intent.AccountHolderName,
                envelope.Intent.RequestedAmount,
                envelope.Channel ?? "Payabo",
                envelope.CountryCode ?? string.Empty);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record GuestDraftEnvelope(
        string? Source,
        string? Channel,
        DateTimeOffset CapturedAt,
        string? CountryCode,
        GuestDraftIntentPayload? Intent);

    private sealed record GuestDraftIntentPayload(
        Guid BillerId,
        string? BillerName,
        Guid ServiceId,
        string ServiceCode,
        string ServiceName,
        Dictionary<string, string>? ServiceFieldValues,
        bool IsValidated,
        string? ValidationMode,
        string? AccountHolderName,
        decimal? RequestedAmount);

    private sealed record GuestDraftIntent(
        Guid BillerId,
        string? BillerName,
        Guid ServiceId,
        string ServiceCode,
        string ServiceName,
        Dictionary<string, string> ServiceFieldValues,
        bool IsValidated,
        DateTimeOffset CapturedAt,
        string? ValidationMode,
        string? AccountHolderName,
        decimal? RequestedAmount,
        string Channel,
        string CountryCode);
}
