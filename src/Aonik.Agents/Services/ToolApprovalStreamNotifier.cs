using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Services;

/// <summary>
/// Default <see cref="IToolApprovalStreamNotifier"/>: a request-scoped, thread-safe buffer of the
/// approval results raised by gated tools during one AG-UI run. The <c>ApprovalGatedAIFunction</c>
/// decorator records into it (even from a nested sub-agent, because it shares the request scope) and
/// <c>AguiStreamPipeline</c> drains it once the run completes (Spec 032 §7.7).
/// </summary>
internal sealed class ToolApprovalStreamNotifier : IToolApprovalStreamNotifier
{
    private readonly object _gate = new();
    private readonly List<object> _results = new();

    public void Record(object approvalResult)
    {
        if (approvalResult is null)
        {
            return;
        }

        lock (_gate)
        {
            _results.Add(approvalResult);
        }
    }

    public IReadOnlyList<object> Drain()
    {
        lock (_gate)
        {
            if (_results.Count == 0)
            {
                return Array.Empty<object>();
            }

            var drained = _results.ToArray();
            _results.Clear();
            return drained;
        }
    }
}
