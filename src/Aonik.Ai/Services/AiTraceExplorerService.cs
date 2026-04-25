using Aonik.Ai.Contracts.Models;
using Microsoft.Extensions.Options;

namespace Aonik.Ai.Services;

internal sealed class AiTraceExplorerService
{
    private readonly IReadOnlyList<IAiTraceReader> _readers;
    private readonly AiTraceExplorerOptions _options;

    public AiTraceExplorerService(
        IEnumerable<IAiTraceReader> readers,
        IOptions<AiTraceExplorerOptions> options)
    {
        _readers = readers.ToList();
        _options = options.Value;
    }

    public async Task<ListAiTraceObservationsResponse> ListObservationsAsync(
        ListAiTraceObservationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var reader = await ResolveReaderAsync(cancellationToken);
        if (reader is null)
        {
            var page = Math.Max(1, request.Page.GetValueOrDefault(1));
            var pageSize = Math.Max(1, Math.Min(_options.MaxPageSize, request.PageSize.GetValueOrDefault(_options.DefaultPageSize)));
            return new ListAiTraceObservationsResponse([], 0, page, pageSize, "None");
        }

        return await reader.ListObservationsAsync(request, cancellationToken);
    }

    private async Task<IAiTraceReader?> ResolveReaderAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.Provider)
            && !_options.Provider.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            var configured = _readers.FirstOrDefault(reader =>
                reader.ProviderName.Equals(_options.Provider, StringComparison.OrdinalIgnoreCase));
            return configured is not null && await configured.IsConfiguredAsync(cancellationToken)
                ? configured
                : null;
        }

        foreach (var reader in _readers)
        {
            if (await reader.IsConfiguredAsync(cancellationToken))
            {
                return reader;
            }
        }

        return null;
    }
}
