namespace Aonik.Cli.Models;

/// <summary>
/// Addresses an anonymous storefront surface. The Spec 066 catalog endpoints are
/// <c>AllowAnonymous</c> and resolve the tenant from the <c>X-Tenant-Id</c> header, so verifying
/// them needs a base URL and a tenant id — no session, no token.
/// </summary>
public sealed record StorefrontTarget(string BaseUrl, Guid TenantId);

public sealed record CliOptionChoice(
    string Key,
    string Label,
    string? Note,
    decimal Price,
    bool IsRecommendedDefault,
    int SortOrder,
    bool IsActive);

public sealed record CliOptionGroup(
    Guid Id,
    string Key,
    string Label,
    string? HelpText,
    string SelectionMode,
    string Currency,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<CliOptionChoice> Choices);

public sealed record CliEffectiveOptionChoice(
    string Key,
    string Label,
    string? Note,
    decimal Price,
    int SortOrder);

public sealed record CliEffectiveOptionGroup(
    string Key,
    string Label,
    string? HelpText,
    string SelectionMode,
    string Currency,
    int SortOrder,
    string DefaultChoiceKey,
    IReadOnlyList<CliEffectiveOptionChoice> Choices);

public sealed record CliStorefrontProduct(
    Guid Id,
    string Slug,
    string Name,
    string Status,
    decimal? UnitSurcharge,
    string? UnitSurchargeCurrency,
    IReadOnlyList<CliEffectiveOptionGroup> EffectiveOptionGroups);

public sealed record CliOptionGroupAdjustment(string GroupKey, IReadOnlyList<string> ChosenKeys, decimal Amount);

public sealed record CliOptionDisplayEntry(string Group, string Choice);

public sealed record CliSelectionQuote(
    string CanonicalSelectionJson,
    bool IsDefault,
    decimal Adjustment,
    string Currency,
    decimal? UnitSurcharge,
    string? UnitSurchargeCurrency,
    string Summary,
    IReadOnlyList<CliOptionDisplayEntry> Display,
    IReadOnlyList<CliOptionGroupAdjustment> Breakdown);

/// <summary>One assertion in <c>commerce verify</c>. <see cref="Passed"/> drives the exit code.</summary>
public sealed record CliVerificationCheck(string Check, string Result, string Detail)
{
    public static CliVerificationCheck Pass(string check, string detail) => new(check, "PASS", detail);

    public static CliVerificationCheck Fail(string check, string detail) => new(check, "FAIL", detail);

    /// <summary>A check that could not run because a prerequisite was absent — reported honestly
    /// rather than silently counted as a pass.</summary>
    public static CliVerificationCheck Skip(string check, string detail) => new(check, "SKIP", detail);

    public bool Passed => Result == "PASS";

    public bool Failed => Result == "FAIL";
}
