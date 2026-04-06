using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Tests.Support;

internal sealed class InMemorySessionStore : ISessionStore
{
    public CliSession? Session { get; private set; }

    public Task<CliSession?> LoadAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult(Session);
    }

    public Task SaveAsync(CliSession session, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        Session = session;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        Session = null;
        return Task.CompletedTask;
    }
}
