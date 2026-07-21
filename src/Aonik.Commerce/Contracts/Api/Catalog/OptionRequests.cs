using System.Text.Json;

namespace Aonik.Commerce.Contracts.Api.Catalog;

/// <summary>HTTP request bodies for the Spec 066 option endpoints. Mapped to service commands.</summary>
public record CreateOptionGroupRequest(
    string Key,
    string Label,
    string? HelpText,
    string? SelectionMode,
    string? Currency,
    int SortOrder);

public record UpdateOptionGroupRequest(
    string Label,
    string? HelpText,
    string? SelectionMode,
    string? Currency,
    int SortOrder,
    bool IsActive);

public record AddOptionChoiceRequest(
    string Key,
    string Label,
    string? Note,
    decimal Price,
    bool IsRecommendedDefault,
    int SortOrder,
    bool IsActive);

public record UpdateOptionChoiceRequest(
    string Label,
    string? Note,
    decimal Price,
    int SortOrder,
    bool IsActive);

public record SetRecommendedDefaultRequest(string ChoiceKey);

public record ProductOptionGroupRequestLine(
    string GroupKey,
    IReadOnlyCollection<string>? AllowedChoiceKeys,
    string? DefaultChoiceKey,
    string? SelectionModeOverride,
    int SortOrder);

public record SetProductOptionGroupsRequest(IReadOnlyCollection<ProductOptionGroupRequestLine>? Groups);

public record SetUnitSurchargeRequest(decimal? Amount, string? Currency);

/// <summary>
/// A selection to price, before any cart exists. <see cref="Currency"/> is <strong>required</strong>:
/// every involved amount must already be denominated in it (rule V10), and without a target there
/// is nothing to validate a multi-currency product's groups against.
/// </summary>
public record SelectionQuoteRequest(JsonElement? Selection, string? Currency);
