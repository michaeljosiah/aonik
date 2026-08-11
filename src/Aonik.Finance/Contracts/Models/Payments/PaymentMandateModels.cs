namespace Aonik.Finance.Contracts.Models.Payments;

// Spec 088 §6 — payment mandate shapes.

public record CreatePaymentMandateRequest(
    Guid PartyId,
    Guid PaymentMethodId,
    string? ProviderMandateRef = null,
    DateTime? ExpiresAt = null);

public record PaymentMandateResponse(
    Guid Id,
    Guid PartyId,
    string Provider,
    Guid PaymentMethodId,
    string? ProviderMandateRef,
    string Status,
    DateTime AuthorisedAt,
    DateTime? ExpiresAt,
    DateTime? RevokedAt,
    string? RevocationReason);
