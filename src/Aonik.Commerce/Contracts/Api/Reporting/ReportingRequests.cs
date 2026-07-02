namespace Aonik.Commerce.Contracts.Api.Reporting;

/// <summary>HTTP request bodies for the reporting endpoints (Spec 057). Mapped to service calls.</summary>
public record SetTargetMarginRequest(decimal? TargetMarginPct);
