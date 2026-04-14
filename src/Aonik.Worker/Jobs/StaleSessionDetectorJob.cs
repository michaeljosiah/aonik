using Aonik.Agents.Contracts.Services;
using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using System.Text.Json;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Quartz job that detects stale chat sessions (no interaction for 15+ minutes)
/// and triggers conversation summary generation.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class StaleSessionDetectorJob : IJob
{
    public static readonly JobKey Key = new("StaleSessionDetectorJob", ScheduledJobGroups.ScheduledJobs);

    private readonly IConversationSummaryService _conversationSummaryService;
    private readonly PlatformDbContext _platformDbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<StaleSessionDetectorJob> _logger;

    public StaleSessionDetectorJob(
        IConversationSummaryService conversationSummaryService,
        PlatformDbContext platformDbContext,
        ITenantContext tenantContext,
        IOptions<ScheduledJobOptions> options,
        ILogger<StaleSessionDetectorJob> logger)
    {
        _conversationSummaryService = conversationSummaryService;
        _platformDbContext = platformDbContext;
        _tenantContext = tenantContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        // Seed system-tenant context up-front. ConversationSummaryGenerator
        // resolves per-thread tenant inside its loop, but the surrounding
        // stale-thread query plus any cache/audit hooks need a context.
        _tenantContext.TenantId = Guid.Empty;
        _tenantContext.ResolutionSource = "system";

        var batchSize = _options.StaleSessionDetector.BatchSize;
        var agentNames = await ResolveAgentNamesAsync(context.CancellationToken);

        _logger.LogDebug(
            "Running stale session detection with batch size {BatchSize} for agents: {AgentNames}.",
            batchSize,
            agentNames.Count > 0 ? string.Join(", ", agentNames) : "(none)");

        await _conversationSummaryService.ProcessStaleSessionsAsync(
            batchSize,
            agentNames,
            context.CancellationToken);

        context.Result = $"Scanned for stale sessions (batch size: {batchSize}, agents: {agentNames.Count}).";
    }

    /// <summary>
    /// Reads agent names from the runtime configuration stored on the projection,
    /// falling back to appsettings if no runtime configuration has been set.
    /// </summary>
    private async Task<List<string>> ResolveAgentNamesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var configJson = await _platformDbContext.ScheduledJobProjections
                .AsNoTracking()
                .Where(x => x.GroupName == ScheduledJobGroups.ScheduledJobs && x.JobName == Key.Name)
                .Select(x => x.ConfigurationJson)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrEmpty(configJson))
            {
                var config = JsonSerializer.Deserialize<StaleSessionDetectorConfiguration>(configJson, JsonOptions);
                if (config?.AgentNames is { Count: > 0 })
                {
                    return config.AgentNames;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read runtime configuration from projection, falling back to appsettings.");
        }

        // Fall back to appsettings
        return _options.StaleSessionDetector.AgentNames;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class StaleSessionDetectorConfiguration
    {
        public List<string> AgentNames { get; set; } = [];
    }
}
