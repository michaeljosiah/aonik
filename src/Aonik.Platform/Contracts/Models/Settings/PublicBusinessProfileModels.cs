namespace Aonik.Platform.Contracts.Models.Settings;

/// <summary>Explicitly public facts, independent of the tenant's administrative contact details.</summary>
public record PublicBusinessProfileDto(
    string DisplayName,
    string? LogoUrl = null,
    string? Website = null,
    PublicBusinessContactDto? Contact = null,
    PublicBusinessLegalDto? Legal = null,
    BusinessOpeningHoursDto? OpeningHours = null);

public record PublicBusinessContactDto(string? Email = null, string? Phone = null, string? WhatsApp = null);

public record PublicBusinessLegalDto(
    string? CompanyName = null,
    string? CompanyNumber = null,
    string? RegisteredOffice = null,
    string? IcoRegistrationNumber = null,
    bool? IsVatRegistered = null,
    string? VatNumber = null);

/// <summary>Local weekly periods; either closure list overrides the weekly schedule for that date.
/// Null hours means unconfigured. An empty weekly schedule means closed all week.</summary>
public record BusinessOpeningHoursDto(
    string Timezone,
    IReadOnlyList<BusinessOpeningPeriodDto> WeeklyHours,
    IReadOnlyList<DateOnly> BankHolidays,
    IReadOnlyList<DateOnly> ExceptionalClosures);

/// <summary>ISO weekday (1 = Monday, 7 = Sunday), with inclusive opening and exclusive closing.
/// Each period is within one local day; multiple non-overlapping periods per day are supported.</summary>
public record BusinessOpeningPeriodDto(int DayOfWeek, TimeOnly OpensAt, TimeOnly ClosesAt);

/// <summary>Stored under Business.PublicProfile through the existing tenant settings endpoint.
/// Publication is opt-in; this wrapper is never returned by the public endpoint.</summary>
public record PublicBusinessProfileDocument(bool IsPublished, PublicBusinessProfileDto? Profile);
