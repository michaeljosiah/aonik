using System.Diagnostics;
using OpenTelemetry;

namespace Aonik.ServiceDefaults;

/// <summary>
/// Copies <see cref="Activity"/> baggage entries whose keys match one of the
/// configured prefixes into span attributes (tags) on every span start.
/// <para>
/// This ensures attributes like <c>langfuse.session.id</c> and
/// <c>langfuse.user.id</c> — set once at the request entry point — propagate
/// to all child spans including those created by MEAI
/// (<c>OpenTelemetryChatClient</c>) and MAF (<c>AgentActivitySource</c>) that
/// cannot be directly instrumented with custom attributes.
/// </para>
/// </summary>
internal sealed class BaggageSpanProcessor : BaseProcessor<Activity>
{
    private readonly string[] _prefixes;

    /// <param name="prefixes">
    /// Baggage key prefixes to copy. Defaults to <c>"langfuse."</c> if none are provided.
    /// </param>
    public BaggageSpanProcessor(params string[] prefixes)
    {
        _prefixes = prefixes.Length > 0 ? prefixes : ["langfuse."];
    }

    public override void OnStart(Activity data)
    {
        foreach (var entry in data.Baggage)
        {
            if (entry.Value is null) continue;

            foreach (var prefix in _prefixes)
            {
                if (entry.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    data.SetTag(entry.Key, entry.Value);
                    break;
                }
            }
        }
    }
}
