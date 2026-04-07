using System.Text.Json;
using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// CRUD endpoints for playground scenarios at <c>/ai/playground/scenarios</c>.
/// </summary>
public static class PlaygroundScenarioEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Maps the playground scenario CRUD endpoints.
    /// </summary>
    public static RouteGroupBuilder MapPlaygroundScenarios(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/ai/playground/scenarios")
    {
        var group = endpoints.MapGroup(prefix)
            .WithTags("AI Playground");

        group.MapGet("/", ListScenarios)
            .WithName("ListPlaygroundScenarios")
            .WithSummary("List playground scenarios for the current tenant");

        group.MapGet("/{id:guid}", GetScenario)
            .WithName("GetPlaygroundScenario")
            .WithSummary("Get a playground scenario by ID, including turns");

        group.MapPost("/", CreateScenario)
            .WithName("CreatePlaygroundScenario")
            .WithSummary("Create a new playground scenario (Save as Scenario)");

        group.MapPut("/{id:guid}", UpdateScenario)
            .WithName("UpdatePlaygroundScenario")
            .WithSummary("Update an existing playground scenario");

        group.MapDelete("/{id:guid}", DeleteScenario)
            .WithName("DeletePlaygroundScenario")
            .WithSummary("Delete a playground scenario");

        return group;
    }

    private static async Task<IResult> ListScenarios(
        HttpContext context,
        string? agentName = null,
        string? tag = null,
        CancellationToken cancellationToken = default)
    {
        var service = context.RequestServices.GetRequiredService<IPlaygroundScenarioService>();
        var scenarios = await service.ListAsync(agentName, tag, cancellationToken);
        return Results.Ok(new { scenarios });
    }

    private static async Task<IResult> GetScenario(
        Guid id,
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        var service = context.RequestServices.GetRequiredService<IPlaygroundScenarioService>();
        var scenario = await service.GetByIdAsync(id, cancellationToken);

        return scenario is null
            ? Results.NotFound(new { message = $"Scenario '{id}' not found" })
            : Results.Ok(scenario);
    }

    private static async Task<IResult> CreateScenario(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        CreatePlaygroundScenarioRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreatePlaygroundScenarioRequest>(
                context.Request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { message = "Invalid request body" });
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "'name' is required" });

        var service = context.RequestServices.GetRequiredService<IPlaygroundScenarioService>();
        var scenario = await service.CreateAsync(request, cancellationToken);

        return Results.Created($"/ai/playground/scenarios/{scenario.Id}", scenario);
    }

    private static async Task<IResult> UpdateScenario(
        Guid id,
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        UpdatePlaygroundScenarioRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdatePlaygroundScenarioRequest>(
                context.Request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { message = "Invalid request body" });
        }

        if (request is null)
            return Results.BadRequest(new { message = "Request body is required" });

        var service = context.RequestServices.GetRequiredService<IPlaygroundScenarioService>();
        var scenario = await service.UpdateAsync(id, request, cancellationToken);

        return scenario is null
            ? Results.NotFound(new { message = $"Scenario '{id}' not found" })
            : Results.Ok(scenario);
    }

    private static async Task<IResult> DeleteScenario(
        Guid id,
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        var service = context.RequestServices.GetRequiredService<IPlaygroundScenarioService>();
        var deleted = await service.DeleteAsync(id, cancellationToken);

        return deleted
            ? Results.NoContent()
            : Results.NotFound(new { message = $"Scenario '{id}' not found" });
    }
}
