using Aonik.IntegrationTests.Support;

namespace Aonik.Database.Tests.Support;

public sealed class WorkflowMigrationFixture : IAsyncLifetime
{
    public SqlLocalDbFixture Database { get; } = new() { ApplyMigrations = true };
    public Task InitializeAsync() => Database.InitializeAsync();
    public Task DisposeAsync() => Database.DisposeAsync();
}
