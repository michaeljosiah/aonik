using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using FastEndpoints;

namespace Aonik.Ai.Endpoints;

internal sealed class ListAiCatalogModelProvidersEndpoint : EndpointWithoutRequest<ListAiCatalogModelProvidersResponse>
{
    private readonly IAiModelCatalogSource _catalogSource;

    public ListAiCatalogModelProvidersEndpoint(IAiModelCatalogSource catalogSource) => _catalogSource = catalogSource;

    public override void Configure()
    {
        Get("/ai/model-catalog/model-providers");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var modelProviders = await _catalogSource.ListModelProvidersAsync(ct);
        await Send.OkAsync(new ListAiCatalogModelProvidersResponse { ModelProviders = modelProviders }, ct);
    }
}

public sealed record ListAiCatalogModelProvidersResponse
{
    public required IReadOnlyList<AiCatalogModelProviderResponse> ModelProviders { get; init; }
}

internal sealed class ListAiCatalogModelsEndpoint : Endpoint<ListAiCatalogModelsRequest, ListAiCatalogModelsResponse>
{
    private readonly IAiModelCatalogSource _catalogSource;

    public ListAiCatalogModelsEndpoint(IAiModelCatalogSource catalogSource) => _catalogSource = catalogSource;

    public override void Configure()
    {
        Get("/ai/model-catalog/model-providers/{ModelProviderKey}/models");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(ListAiCatalogModelsRequest req, CancellationToken ct)
    {
        var modelProvider = await _catalogSource.GetModelProviderAsync(req.ModelProviderKey, ct);
        if (modelProvider is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var models = await _catalogSource.ListModelsAsync(req.ModelProviderKey, ct);
        await Send.OkAsync(new ListAiCatalogModelsResponse { Models = models }, ct);
    }
}

public sealed record ListAiCatalogModelsRequest
{
    public string ModelProviderKey { get; init; } = string.Empty;
}

public sealed record ListAiCatalogModelsResponse
{
    public required IReadOnlyList<AiCatalogModelResponse> Models { get; init; }
}

internal sealed class ImportAiCatalogModelProviderEndpoint : Endpoint<ImportAiCatalogModelProviderEndpointRequest, ImportAiCatalogModelProviderResponse>
{
    private readonly IAiModelCatalogImportService _catalogImportService;
    private readonly IAiModelCatalogSource _catalogSource;

    public ImportAiCatalogModelProviderEndpoint(
        IAiModelCatalogImportService catalogImportService,
        IAiModelCatalogSource catalogSource)
    {
        _catalogImportService = catalogImportService;
        _catalogSource = catalogSource;
    }

    public override void Configure()
    {
        Post("/ai/model-catalog/model-providers/{ModelProviderKey}/import");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(ImportAiCatalogModelProviderEndpointRequest req, CancellationToken ct)
    {
        var modelProvider = await _catalogSource.GetModelProviderAsync(req.ModelProviderKey, ct);
        if (modelProvider is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var response = await _catalogImportService.ImportModelProviderAsync(
            req.ModelProviderKey,
            new ImportAiCatalogModelProviderRequest
            {
                ImportModelsAsInactive = req.ImportModelsAsInactive,
            },
            ct);

        await Send.OkAsync(response, ct);
    }
}

public sealed record ImportAiCatalogModelProviderEndpointRequest
{
    public string ModelProviderKey { get; init; } = string.Empty;
    public bool ImportModelsAsInactive { get; init; } = true;
}
