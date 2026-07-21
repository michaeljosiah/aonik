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

/// <summary>
/// Update requests use nullable value types throughout: an omitted JSON property binds to the CLR
/// default, and for these members the CLR default is destructive — <c>false</c> deactivates,
/// <c>0</c> reprices or reorders. Null means "leave unchanged"; only supplied values apply.
/// </summary>
public record UpdateOptionGroupRequest(
    string Label,
    string? HelpText = null,
    string? SelectionMode = null,
    string? Currency = null,
    int? SortOrder = null,
    bool? IsActive = null);

/// <summary>Creation defaults are explicit so a minimal <c>{key,label}</c> payload produces a
/// usable choice. <c>IsActive</c> in particular must not fall to the CLR default: an omission
/// would create an invisible choice, and — if it was also the group's first recommended default —
/// a group that silently never becomes servable.</summary>
public record AddOptionChoiceRequest(
    string Key,
    string Label,
    string? Note = null,
    decimal Price = 0m,
    bool IsRecommendedDefault = false,
    int SortOrder = 0,
    bool? IsActive = null);

public record UpdateOptionChoiceRequest(
    string Label,
    string? Note = null,
    decimal? Price = null,
    int? SortOrder = null,
    bool? IsActive = null);

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
