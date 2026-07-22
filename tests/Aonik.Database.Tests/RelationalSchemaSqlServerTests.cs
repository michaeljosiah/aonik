using Aonik.IntegrationTests.Support;

using FluentAssertions;

using Microsoft.Data.SqlClient;

namespace Aonik.Database.Tests;

/// <summary>
/// Smoke checks that the full <c>AonikDbContext</c> model materialises on a real
/// SQL Server engine. The fixture's <c>EnsureCreated</c> already did the heavy
/// lifting — these tests exist so a relationally invalid model (bad column
/// type, duplicate index name, over-long key) fails HERE with a schema error
/// rather than as collateral noise inside a behavioural test.
/// </summary>
public class RelationalSchemaSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;

    public RelationalSchemaSqlServerTests(SqlLocalDbFixture db)
    {
        _db = db;
    }

    [SkippableFact]
    public async Task FullAonikModel_Should_CreateOnSqlServer()
    {
        RequireSqlServer();

        (await ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables")).Should().BeGreaterThan(50,
            "EnsureCreated must have materialised the whole modular-monolith model");
        (await ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name IN ('AnkOptionChoices', 'AnkProducts')"))
            .Should().Be(2);
    }

    [SkippableFact]
    public async Task RowVersion_Should_MapToNativeRowversionColumn()
    {
        RequireSqlServer();

        // ConfigureRowVersions maps AuditableEntity.RowVersion to the engine's
        // rowversion type (reported as 'timestamp' in sys.types). If this ever
        // regresses to a plain varbinary, updates stop bumping the token and
        // every optimistic-concurrency guarantee silently dies.
        var typeName = await ScalarAsync<string>("""
            SELECT ty.name
            FROM sys.columns c
            JOIN sys.tables t ON t.object_id = c.object_id
            JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            WHERE t.name = 'AnkOptionGroups' AND c.name = 'RowVersion'
            """);

        typeName.Should().Be("timestamp");
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return (T)result!;
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
