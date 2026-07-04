using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Ai.Endpoints;

internal sealed class ListAiCatalogModelProvidersEndpoint : EndpointWithoutRequest<ListAiCatalogModelProvidersResponse>
{
    private readonly IAiModelCatalogSource _catalogSource;

    public ListAiCatalogModelProvidersEndpoint(IAiModelCatalogSource catalogSource) => _catalogSource = catalogSource;

    public override void Configure()
    {
        Get("/ai/model-catalog/model-providers");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List catalog model providers";
            s.Description = "Returns all model providers available in the AI model catalog for discovery and import.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("AI Configuration"));
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
        Summary(s =>
        {
            s.Summary = "List catalog models for a provider";
            s.Description = "Returns all models available in the catalog for a specific model provider.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
            s.Response(404, "Model provider not found");
        });
        Options(x => x.WithTags("AI Configuration"));
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
        Policies("AdminWritePolicy");
        Summary(s =>
        {
            s.Summary = "Import a catalog model provider";
            s.Description = "Imports a model provider and its models from the catalog into the active AI configuration.";
            s.Response(200, "Import completed");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(404, "Model provider not found in catalog");
        });
        Options(x => x.WithTags("AI Configuration"));
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
