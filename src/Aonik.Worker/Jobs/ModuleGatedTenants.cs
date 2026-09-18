using Aonik.SharedKernel.Modules;

using Microsoft.Extensions.Logging;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Narrows a job's tenant fan-out to the tenants that have the job's module enabled
/// (Spec 097 §12.2). The job still fires on schedule; it simply does nothing for a tenant whose
/// module is off, and reports how many it skipped in its execution result.
/// </summary>
/// <remarks>
/// A null module id, a core module, an unknown id or a missing reader all mean "no gate": the
/// original list is returned untouched. The reader is optional so job unit tests that construct
/// jobs directly keep working without registering Platform.
/// </remarks>
internal static class ModuleGatedTenants
{
    internal readonly record struct Result(IReadOnlyList<Guid> Enabled, int Skipped, string? ModuleId)
    {
        /// <summary>No gate was evaluated: nothing enabled, nothing skipped.</summary>
        public static Result None { get; } = new([], 0, null);

        /// <summary>A sentence for the job's execution result, or an empty string when nothing was skipped.</summary>
        public string Note => Skipped == 0
            ? string.Empty
            : $" Skipped {Skipped} tenant(s) with module '{ModuleId}' disabled.";
    }

    public static bool IsGated(string? moduleId)
        => moduleId is not null && ModuleCatalog.IsKnown(moduleId) && !ModuleCatalog.CoreIds.Contains(moduleId);

    public static async Task<Result> FilterAsync(
        IModuleEnablementReader? reader,
        IReadOnlyList<Guid> tenantIds,
        string? moduleId,
        string jobName,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantIds);

        if (reader is null || !IsGated(moduleId) || tenantIds.Count == 0)
            return new Result(tenantIds, 0, moduleId);

        var enabled = await reader.FilterEnabledTenantsAsync(tenantIds, moduleId!, cancellationToken);
        var skipped = tenantIds.Distinct().Count() - enabled.Count;

        if (skipped > 0)
        {
            logger.LogInformation(
                "{JobName} skipped {Skipped} tenant(s) because module '{ModuleId}' is disabled for them.",
                jobName, skipped, moduleId);
        }

        return new Result(enabled, skipped, moduleId);
    }
}
