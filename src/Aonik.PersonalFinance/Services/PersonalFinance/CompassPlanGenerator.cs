using System.Text.Json;
using Aonik.Finance.Agents.StructuredOutputs;
using Aonik.SharedKernel.Abstractions.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Finance.Services.PersonalFinance;

/// <summary>
/// Default <see cref="ICompassPlanGenerator"/> — invokes the registered
/// <c>pf-compass-planner</c> <see cref="IDomainAgentDescriptor"/> as a
/// structured-output agent, mirroring the sub-agent invocation pattern used by
/// <c>PersonalFinanceTools</c> (descriptor resolution → tenant config override →
/// <c>RunAsync&lt;T&gt;</c> with the schema serializer options).
/// </summary>
internal sealed class CompassPlanGenerator : ICompassPlanGenerator
{
    private const string PlannerAgentName = "pf-compass-planner";

    private readonly IServiceProvider _serviceProvider;
    private readonly IChatClient _chatClient;
    private readonly IAgentConfigurationService _agentConfigurationService;

    public CompassPlanGenerator(
        IServiceProvider serviceProvider,
        IChatClient chatClient,
        IAgentConfigurationService agentConfigurationService)
    {
        _serviceProvider = serviceProvider;
        _chatClient = chatClient;
        _agentConfigurationService = agentConfigurationService;
    }

    public async Task<CompassPlannerAgentToolResponse> GenerateAsync(
        CompassPlannerRequest request,
        CancellationToken cancellationToken = default)
    {
        var message = JsonSerializer.Serialize(request, CompassPlannerStructuredOutputContract.SerializerOptions);

        var descriptor = ResolveDescriptor(PlannerAgentName);
        var agent = await BuildStructuredAgentAsync(descriptor, cancellationToken);

        var response = await agent.RunAsync<CompassPlanResult>(
            message,
            session: null,
            serializerOptions: CompassPlannerStructuredOutputContract.SerializerOptions,
            options: null,
            cancellationToken: cancellationToken);

        var plan = response.Result;
        var planJson = JsonSerializer.Serialize(plan, CompassPlannerStructuredOutputContract.SerializerOptions);
        return new CompassPlannerAgentToolResponse(plan, planJson);
    }

    private IDomainAgentDescriptor ResolveDescriptor(string name)
    {
        var descriptor = _serviceProvider
            .GetServices<IDomainAgentDescriptor>()
            .FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal));

        return descriptor
            ?? throw new InvalidOperationException(
                $"The '{name}' sub-agent descriptor is not registered in DI. Check FinanceModule.ConfigureServices.");
    }

    private async Task<ChatClientAgent> BuildStructuredAgentAsync(
        IDomainAgentDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var config = await _agentConfigurationService.GetResolvedAsync(descriptor.Name, cancellationToken);

        string? instructionsOverride = null;
        if (config is not null && !string.IsNullOrWhiteSpace(config.InstructionsText))
        {
            instructionsOverride = config.InstructionsText;
        }

        var builtAgent = config is null
            ? descriptor.Build(_chatClient, _serviceProvider)
            : descriptor.Build(_chatClient, _serviceProvider, instructionsOverride, allowedToolNames: null);

        return builtAgent as ChatClientAgent
            ?? throw new InvalidOperationException($"The agent '{descriptor.Name}' must be a ChatClientAgent.");
    }
}
