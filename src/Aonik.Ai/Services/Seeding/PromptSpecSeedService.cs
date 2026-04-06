using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services.Seeding;

/// <summary>
/// Seeds global (tenant-agnostic) <see cref="PromptSpec"/> rows from file-based templates.
/// Only inserts specs that don't already exist (matched by Name + Version + TenantId = null).
/// Existing rows are updated if the file-based template content has changed.
/// Idempotent and safe to call on every startup.
/// Tenants can override these via the <see cref="TenantAwarePromptStore"/> resolution chain.
/// </summary>
internal class PromptSpecSeedService
{
    private readonly AiDbContext _dbContext;
    private readonly FileBasedPromptStore _fileStore;
    private readonly ILogger<PromptSpecSeedService> _logger;

    public PromptSpecSeedService(
        AiDbContext dbContext,
        FileBasedPromptStore fileStore,
        ILogger<PromptSpecSeedService> logger)
    {
        _dbContext = dbContext;
        _fileStore = fileStore;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting prompt spec seed process...");

        var definitions = GetPromptDefinitions();

        var existing = await _dbContext.PromptSpecs
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == null)
            .ToListAsync(cancellationToken);

        var existingByKey = existing.ToDictionary(
            p => $"{p.Name}|{p.Version}",
            StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var updated = 0;

        foreach (var def in definitions)
        {
            var systemTemplate = await TryLoadTemplateAsync(def.Name, def.Version, "system", cancellationToken);
            var userTemplate = await TryLoadTemplateAsync(def.Name, def.Version, "user", cancellationToken);

            if (string.IsNullOrEmpty(systemTemplate) && string.IsNullOrEmpty(userTemplate))
            {
                _logger.LogWarning(
                    "No file-based templates found for prompt '{Name}' v{Version} — skipping",
                    def.Name, def.Version);
                continue;
            }

            var key = $"{def.Name}|{def.Version}";

            if (existingByKey.TryGetValue(key, out var existingSpec))
            {
                var changed = false;

                if (!string.Equals(existingSpec.SystemTemplate, systemTemplate ?? string.Empty, StringComparison.Ordinal))
                {
                    existingSpec.SystemTemplate = systemTemplate ?? string.Empty;
                    changed = true;
                }

                if (!string.Equals(existingSpec.UserTemplate, userTemplate ?? string.Empty, StringComparison.Ordinal))
                {
                    existingSpec.UserTemplate = userTemplate ?? string.Empty;
                    changed = true;
                }

                if (changed)
                    updated++;
            }
            else
            {
                _dbContext.PromptSpecs.Add(new PromptSpec
                {
                    TenantId = null,
                    Name = def.Name,
                    Version = def.Version,
                    SystemTemplate = systemTemplate ?? string.Empty,
                    UserTemplate = userTemplate ?? string.Empty,
                    DeveloperTemplate = string.Empty,
                    VariablesSchemaJson = def.VariablesSchemaJson ?? string.Empty,
                    OutputSchemaJson = def.OutputSchemaJson ?? string.Empty,
                    IsPublished = true,
                });

                added++;
            }
        }

        if (added > 0 || updated > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Prompt spec seed completed (added {Added}, updated {Updated})",
                added, updated);
        }
        else
        {
            _logger.LogInformation("All prompt specs already up to date — skipping seed");
        }
    }

    private async Task<string?> TryLoadTemplateAsync(
        string name, string version, string role, CancellationToken cancellationToken)
    {
        try
        {
            return await _fileStore.LoadPromptAsync(name, version, role, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static List<PromptDefinition> GetPromptDefinitions() =>
    [
        new("transaction_classification", "v1",
            VariablesSchemaJson: """{"TRANSACTIONS_JSON": "JSON array of transactions to classify"}"""),

        new("personal_spending_insight", "v1",
            VariablesSchemaJson: """{"SPENDING_DATA": "Spending summary data as JSON"}"""),

        new("customer_insight_summary", "v2",
            VariablesSchemaJson: """{"SNAPSHOT_JSON": "Deterministic customer insight snapshot as JSON"}""",
            OutputSchemaJson: CustomerInsightAiSummaryContract.SummaryJsonSchema),

        new("invoice_insight", "v1",
            VariablesSchemaJson: """{"INVOICE_DATA": "Invoice details as JSON"}"""),

        new("thread_title", "v1",
            VariablesSchemaJson: """{"message": "User message to generate a title for"}"""),

        new("conversation_summary", "v1"),

        new("orchestrator", "v1"),
    ];

    private sealed record PromptDefinition(
        string Name,
        string Version,
        string? VariablesSchemaJson = null,
        string? OutputSchemaJson = null);
}
