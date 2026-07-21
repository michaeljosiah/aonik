using Aonik.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aonik.IntegrationTests.Support;

/// <summary>
/// Provides a throwaway SQL Server LocalDB database per test class, created with
/// <c>EnsureCreated</c> from the canonical <see cref="AonikDbContext"/> model and
/// dropped again when the class finishes.
///
/// This is the relational-semantics lane. The EF Core InMemory provider is
/// non-relational, so three whole classes of defect are invisible to the main
/// suite: user-initiated transactions under a retrying execution strategy
/// (<c>IsRelational()</c> is false, so the failing path is never reached),
/// optimistic-concurrency conflicts on the <c>rowversion</c> tokens configured by
/// <c>AonikDbContextBase.ConfigureRowVersions</c>, and filtered unique indexes,
/// which only the engine enforces. Spec 066 shipped a P1 that failed on every
/// SQL Server call while 2,000+ InMemory tests passed — this fixture exists so
/// that class of defect has somewhere to fail.
///
/// Complements <see cref="SqlServerContainerFixture"/> rather than replacing it:
/// the container lane applies the real migration stream (schema fidelity, needs
/// Docker), while this lane materialises the CURRENT model via EnsureCreated —
/// no migration history, no SQL applied outside the model — and needs only the
/// LocalDB feature that ships with Visual Studio / SQL Server Express. When
/// LocalDB is unavailable (non-Windows CI, bare build agents) the fixture
/// records <see cref="SkipReason"/> instead of throwing, so consuming tests can
/// <c>Skip.IfNot(fixture.IsAvailable, ...)</c> and the suite stays green.
///
/// Isolation contract: one database per fixture instance (xUnit creates one
/// instance per test class using <c>IClassFixture</c>), and tests inside a class
/// isolate from each other by minting a fresh TenantId each — the same
/// convention the InMemory suite uses. If a run is killed hard, the orphaned
/// database is identifiable by the <c>AonikDatabaseTests_</c> prefix on the
/// instance.
///
/// CI / non-Windows: set <c>AONIK_SQLSERVER_TEST_CONNECTION</c> to a
/// SERVER-level connection string (credentials with CREATE/DROP DATABASE
/// rights; any Initial Catalog in it is ignored — the fixture swaps in its
/// per-class database name) and the fixture targets that SQL Server instead of
/// LocalDB — this is how the ubuntu CI leg runs the lane against a SQL Server
/// service container. When the variable is set there is deliberately NO skip
/// path: the operator explicitly demanded relational coverage, so an
/// unreachable server fails the lane loudly rather than skipping into a false
/// all-clear.
/// </summary>
public sealed class SqlLocalDbFixture : IAsyncLifetime
{
    private const string InstanceDataSource = @"(localdb)\MSSQLLocalDB";

    private const string OverrideVariable = "AONIK_SQLSERVER_TEST_CONNECTION";

    private static readonly string? OverrideConnectionString =
        Environment.GetEnvironmentVariable(OverrideVariable) is { Length: > 0 } value ? value : null;

    // Probed once per test process, not per fixture: when LocalDB is installed the
    // first connection may cold-start the instance (seconds), and when it is not
    // installed every probe would pay the same failure. Both are worth caching.
    private static readonly Lazy<Task<string?>> InstanceProbe = new(
        ProbeInstanceAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly string _databaseName = $"AonikDatabaseTests_{Guid.NewGuid():N}";
    private bool _databaseCreated;

    /// <summary>True once the database exists with the full Aonik model applied.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Human-readable reason LocalDB is unavailable, surfaced in test Skip
    /// messages. Null while the fixture is available.
    /// </summary>
    public string? SkipReason { get; private set; }

    /// <summary>Connection string to this fixture's database. Throws when unavailable.</summary>
    public string ConnectionString =>
        IsAvailable
            ? BuildConnectionString(_databaseName)
            : throw new InvalidOperationException($"SQL Server LocalDB is not available: {SkipReason}");

    /// <summary>
    /// Options for a module context over this fixture's database, configured
    /// exactly as the module composition roots configure them in production —
    /// <c>UseSqlServer</c> WITH <c>EnableRetryOnFailure</c>. The retrying
    /// execution strategy is not an implementation detail here: it is the very
    /// thing that makes a bare <c>BeginTransactionAsync</c> throw, so tests must
    /// run under it to catch that regression class.
    /// </summary>
    public DbContextOptions<TContext> CreateOptions<TContext>()
        where TContext : DbContext
        => new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(ConnectionString, sql => sql.EnableRetryOnFailure())
            .Options;

    public async Task InitializeAsync()
    {
        // The LocalDB availability probe (and its skip semantics) applies only
        // when no override is configured. With AONIK_SQLSERVER_TEST_CONNECTION
        // set, an unreachable server surfaces as a hard failure from
        // EnsureCreated below — never a skip.
        if (OverrideConnectionString is null)
        {
            SkipReason = await InstanceProbe.Value;
            if (SkipReason is not null)
            {
                return;
            }
        }

        // IsAvailable must be set before CreateOptions (which guards on it); if
        // EnsureCreated itself fails the model is relationally broken and the
        // whole class SHOULD fail loudly — that is a defect this lane exists to
        // surface, never something to skip past.
        IsAvailable = true;
        await using var context = new AonikDbContext(CreateOptions<AonikDbContext>());

        // Record intent BEFORE materialising: on a relationally invalid model,
        // EnsureCreated throws AFTER the physical database exists, and Dispose
        // must still drop the partial database rather than orphan one per
        // failing run. When creation failed before the database existed,
        // EnsureDeleted is a safe no-op.
        _databaseCreated = true;
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (!_databaseCreated)
        {
            return;
        }

        try
        {
            // Pooled connections from the tests would otherwise hold the database
            // open and turn the drop into an "in use" error.
            SqlConnection.ClearAllPools();
            await using var context = new AonikDbContext(CreateOptions<AonikDbContext>());
            await context.Database.EnsureDeletedAsync();
        }
        catch (Exception)
        {
            // Best effort: a failed drop must not fail the test run. Orphans carry
            // the AonikDatabaseTests_ prefix and can be dropped manually.
        }
    }

    private static async Task<string?> ProbeInstanceAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "SQL Server LocalDB is only available on Windows.";
        }

        try
        {
            await using var connection = new SqlConnection(BuildConnectionString("master"));
            await connection.OpenAsync();
            return null;
        }
        catch (Exception ex)
        {
            // Not installed, instance broken, or the auto-start timed out.
            return $"SQL Server LocalDB is not available ({ex.GetType().Name}: {ex.Message}).";
        }
    }

    private static string BuildConnectionString(string database)
    {
        if (OverrideConnectionString is not null)
        {
            // Server-level override (CI service container, a full local SQL
            // Server, …): keep every supplied setting, swap in our database.
            return new SqlConnectionStringBuilder(OverrideConnectionString)
            {
                InitialCatalog = database,
            }.ConnectionString;
        }

        return new SqlConnectionStringBuilder
        {
            DataSource = InstanceDataSource,
            InitialCatalog = database,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            // Generous: the first connection after boot cold-starts the LocalDB
            // instance, which can take well over the 15s default.
            ConnectTimeout = 60,
        }.ConnectionString;
    }
}
