using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>Authoring endpoints for the Spec 066 option catalogue.</summary>
public class CreateOptionGroupEndpoint : Endpoint<CreateOptionGroupRequest, OptionGroupDto>
{
    private readonly IProductOptionService _options;

    public CreateOptionGroupEndpoint(IProductOptionService options) => _options = options;

    public override void Configure()
    {
        Post("/commerce/admin/option-groups");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Create an option group.");
    }

    public override async Task HandleAsync(CreateOptionGroupRequest req, CancellationToken ct)
    {
        var result = await _options.CreateGroupAsync(
            new CreateOptionGroupCommand(
                req.Key,
                req.Label,
                req.HelpText,
                req.SelectionMode ?? Entities.Catalog.OptionSelectionModes.One,
                req.Currency ?? "GBP",
                req.SortOrder),
            ct);

        await Send.OkAsync(result, ct);
    }
}

public class UpdateOptionGroupEndpoint : Endpoint<UpdateOptionGroupRequest, OptionGroupDto>
{
    private readonly IProductOptionService _options;

    public UpdateOptionGroupEndpoint(IProductOptionService options) => _options = options;

    public override void Configure()
    {
        Put("/commerce/admin/option-groups/{groupId:guid}");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Update an option group. The key is immutable.");
    }

    public override async Task HandleAsync(UpdateOptionGroupRequest req, CancellationToken ct)
    {
        var result = await _options.UpdateGroupAsync(
            Route<Guid>("groupId"),
            new UpdateOptionGroupCommand(
                req.Label,
                req.HelpText,
                req.SelectionMode ?? Entities.Catalog.OptionSelectionModes.One,
                req.Currency ?? "GBP",
                req.SortOrder,
                req.IsActive),
            ct);

        await Send.OkAsync(result, ct);
    }
}

public class ListOptionGroupsEndpoint : EndpointWithoutRequest<IReadOnlyList<OptionGroupDto>>
{
    private readonly IProductOptionService _options;

    public ListOptionGroupsEndpoint(IProductOptionService options) => _options = options;

    public override void Configure()
    {
        Get("/commerce/admin/option-groups");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "List option groups, including inactive and half-authored ones.");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _options.GetCatalogueAsync(includeInactive: true, ct), ct);
}

public class AddOptionChoiceEndpoint : Endpoint<AddOptionChoiceRequest, OptionChoiceDto>
{
    private readonly IProductOptionService _options;

    public AddOptionChoiceEndpoint(IProductOptionService options) => _options = options;

    public override void Configure()
    {
        Post("/commerce/admin/option-groups/{groupId:guid}/choices");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Add a choice to an option group.");
    }

    public override async Task HandleAsync(AddOptionChoiceRequest req, CancellationToken ct)
    {
        var result = await _options.AddChoiceAsync(
            Route<Guid>("groupId"),
            new AddOptionChoiceCommand(req.Key, req.Label, req.Note, req.Price, req.IsRecommendedDefault, req.SortOrder, req.IsActive),
            ct);

        await Send.OkAsync(result, ct);
    }
}

public class UpdateOptionChoiceEndpoint : Endpoint<UpdateOptionChoiceRequest, OptionChoiceDto>
{
    private readonly IProductOptionService _options;

    public UpdateOptionChoiceEndpoint(IProductOptionService options) => _options = options;

    public override void Configure()
    {
        Put("/commerce/admin/option-choices/{choiceId:guid}");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Update a choice. The key and the default flag are not editable here.");
    }

    public override async Task HandleAsync(UpdateOptionChoiceRequest req, CancellationToken ct)
    {
        var result = await _options.UpdateChoiceAsync(
            Route<Guid>("choiceId"),
            new UpdateOptionChoiceCommand(req.Label, req.Note, req.Price, req.SortOrder, req.IsActive),
            ct);

        await Send.OkAsync(result, ct);
    }
}

/// <summary>
/// Moves a group's recommended default atomically. The only supported way to change it — direct
/// flag writes are rejected (rule V7) because a demote-then-promote pair would transit through a
/// zero- or two-default state.
/// </summary>
public class SetRecommendedDefaultEndpoint : Endpoint<SetRecommendedDefaultRequest, OptionGroupDto>
{
    private readonly IProductOptionService _options;

    public SetRecommendedDefaultEndpoint(IProductOptionService options) => _options = options;

    public override void Configure()
    {
        Put("/commerce/admin/option-groups/{groupId:guid}/recommended-default");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Atomically move a group's recommended default to another choice.");
    }

    public override async Task HandleAsync(SetRecommendedDefaultRequest req, CancellationToken ct)
    {
        var result = await _options.SetRecommendedDefaultAsync(Route<Guid>("groupId"), req.ChoiceKey, ct);
        await Send.OkAsync(result, ct);
    }
}
