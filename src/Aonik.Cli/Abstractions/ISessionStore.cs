using Aonik.Cli.Models;

namespace Aonik.Cli.Abstractions;

public interface ISessionStore
{
    Task<CliSession?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CliSession session, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
