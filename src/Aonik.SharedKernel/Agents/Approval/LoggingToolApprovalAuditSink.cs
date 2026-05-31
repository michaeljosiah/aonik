using Aonik.SharedKernel.Abstractions.Agents;

using Microsoft.Extensions.Logging;

namespace Aonik.SharedKernel.Agents.Approval;

/// <summary>
/// Default <see cref="IToolApprovalAuditSink"/> that writes every gated-tool outcome to the
/// logger. Blocked Medium/High attempts are logged at <see cref="LogLevel.Warning"/> so an
/// ungated mutation attempt is visible in operational telemetry; Low in-band executions are
/// logged at <see cref="LogLevel.Information"/>.
/// </summary>
public sealed class LoggingToolApprovalAuditSink : IToolApprovalAuditSink
{
    private readonly ILogger<LoggingToolApprovalAuditSink> _logger;

    public LoggingToolApprovalAuditSink(ILogger<LoggingToolApprovalAuditSink> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Record(ToolApprovalAuditEntry entry)
    {
        if (entry.Executed)
        {
            _logger.LogInformation(
                "AI tool approval gate: '{Tool}' (tier {Tier}) executed in-band ({Outcome}).",
                entry.Tool, entry.Tier, entry.Outcome);
        }
        else
        {
            _logger.LogWarning(
                "AI tool approval gate: '{Tool}' (tier {Tier}) BLOCKED — requires approval, not executed ({Outcome}).",
                entry.Tool, entry.Tier, entry.Outcome);
        }
    }
}
