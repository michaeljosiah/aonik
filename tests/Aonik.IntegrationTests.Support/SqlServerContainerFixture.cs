using Aonik.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
using Respawn.Graph;
using Testcontainers.MsSql;
using Xunit;

namespace Aonik.IntegrationTests.Support;

/// <summary>
/// Spins up a real SQL Server in a Docker container for the lifetime of a test
/// collection, applies the canonical <see cref="AonikDbContext"/> migration
/// stream once, and resets row data between tests with Respawn.
///
/// Financial-path tests need true relational fidelity — real transactions,
/// unique indexes, decimal precision, tenant query-filter SQL — that the EF Core
/// InMemory provider cannot reproduce. This fixture is the seam that lets those
/// tests run against the same schema production gets.
///
/// Docker is a hard prerequisite. When the daemon is unreachable the fixture
/// records <see cref="SkipReason"/> instead of throwing, so tests can
/// <c>Skip.IfNot(fixture.IsAvailable, ...)</c> and the suite stays green on
/// machines (and CI legs) without Docker.
/// </summary>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    /// <summary>
    /// Shared collection name. xUnit collection definitions are per-assembly, so
    /// each consuming test project declares its own one-line
    /// <c>[CollectionDefinition(SqlServerContainerFixture.CollectionName)]</c>
    /// over this fixture; the constant keeps the string in one place.
    /// </summary>
    public const string CollectionName = "SqlServer integration";

    // Built inside InitializeAsync, not in a field initializer: Testcontainers 4.x
    // resolves the Docker endpoint during Build(), so constructing here would throw
    // in the fixture's constructor — before InitializeAsync's try/catch — turning a
    // Docker-down run into a hard failure instead of a clean skip.
    private MsSqlContainer? _container;
    private Respawner? _respawner;

    /// <summary>True once the container started and migrations applied.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Human-readable reason the container is unavailable, surfaced in test Skip
    /// messages. Null while the fixture is available.
    /// </summary>
    public string? SkipReason { get; private set; }

    /// <summary>Connection string to the migrated database. Throws when unavailable.</summary>
    public string ConnectionString =>
        IsAvailable
            ? _container!.GetConnectionString()
            : throw new InvalidOperationException(
                $"SQL Server container is not available: {SkipReason}");

    public async Task InitializeAsync()
    {
        try
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync();
        }
        catch (Exception ex)
        {
            // Docker not installed, daemon not running, or image pull blocked.
            // Record the reason and let tests skip rather than fail the run.
            SkipReason = $"Docker is not available ({ex.GetType().Name}: {ex.Message}).";
            return;
        }

        await ApplyMigrationsAsync();
        _respawner = await CreateRespawnerAsync();
        IsAvailable = true;
    }

    /// <summary>
    /// Deletes all row data while preserving the schema and migration history, so
    /// a shared container hands every test a clean slate without re-running the
    /// whole migration stream. Call at the start of each test.
    /// </summary>
    public async Task ResetAsync()
    {
        if (!IsAvailable || _respawner is null)
        {
            return;
        }

        await using var connection = new SqlConnection(_container!.GetConnectionString());
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private async Task ApplyMigrationsAsync()
    {
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseSqlServer(_container!.GetConnectionString())
            .Options;

        await using var context = new AonikDbContext(options);
        await context.Database.MigrateAsync();
    }

    private async Task<Respawner> CreateRespawnerAsync()
    {
        await using var connection = new SqlConnection(_container!.GetConnectionString());
        await connection.OpenAsync();

        return await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = ["dbo"],
            // Never delete EF's migration ledger — the schema is migrated once at
            // startup and must survive every reset.
            TablesToIgnore = [new Table("__EFMigrationsHistory")],
        });
    }
}
