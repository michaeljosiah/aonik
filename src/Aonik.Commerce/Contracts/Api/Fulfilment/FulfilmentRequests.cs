namespace Aonik.Commerce.Contracts.Api.Fulfilment;

public record UpsertFulfilmentCalendarRequest(
    string Timezone,
    List<string>? DeliveryDays,
    TimeOnly CutoffLocalTime,
    string? CutoffDayOfWeek,
    int LeadDays,
    List<string>? BlackoutDates,
    bool IsActive);
