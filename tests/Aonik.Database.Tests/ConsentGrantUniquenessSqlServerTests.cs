using Aonik.IntegrationTests.Support;

using FluentAssertions;

using Microsoft.Data.SqlClient;

namespace Aonik.Database.Tests;

/// <summary>
/// Spec 095 §13. The one-active-grant-per-purpose rule is a <em>filtered unique index</em>, and the
/// InMemory provider enforces neither filters nor uniqueness — so a test there would pass while the
/// invariant did not exist. This lane is where it can actually fail.
///
/// <para>
/// What the index protects: <c>IConsentReader</c> asks only about subject and purpose. If two active
/// grants could coexist for one pair, a stale grant under superseded terms would keep authorising
/// processing, and a material terms change would invalidate nothing.
/// </para>
/// </summary>
public class ConsentGrantUniquenessSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;

    public ConsentGrantUniquenessSqlServerTests(SqlLocalDbFixture db)
    {
        _db = db;
    }

    [SkippableFact]
    public async Task ActiveGrantIndex_Should_BeUniqueAndFilteredOnRevokedAt()
    {
        RequireSqlServer();

        var (isUnique, filter) = await IndexShapeAsync(
            "AnkConsentGrants", "IX_AnkConsentGrants_TenantId_SubjectPartyId_Purpose");

        isUnique.Should().BeTrue("two active grants for one subject and purpose must be impossible");
        filter.Should().Contain("RevokedAt",
            "the index must be filtered on RevokedAt, so a revoked grant does not block a re-grant");
    }

    [SkippableFact]
    public async Task ActiveGrantIndex_Should_NotIncludeTermsVersion()
    {
        RequireSqlServer();

        // Including TermsVersion in the key would permit an active v1 AND an active v2 for the same
        // subject and purpose. Since the reader is version-agnostic by design, it would find the
        // stale v1 and authorise -- so a material terms change would silently invalidate nothing.
        var columns = await IndexColumnsAsync(
            "AnkConsentGrants", "IX_AnkConsentGrants_TenantId_SubjectPartyId_Purpose");

        columns.Should().NotContain("TermsVersion",
            "version-in-the-key is exactly the defect this index shape exists to prevent");
        columns.Should().BeEquivalentTo(new[] { "TenantId", "SubjectPartyId", "Purpose" });
    }

    [SkippableFact]
    public async Task SecondActiveGrant_Should_BeRejectedByTheEngine()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var guardianId = Guid.NewGuid();

        await InsertGrantAsync(tenantId, subjectId, guardianId, "service-core", "v1", revoked: false);

        // Same subject, same purpose, different terms version, still active.
        var second = async () => await InsertGrantAsync(
            tenantId, subjectId, guardianId, "service-core", "v2", revoked: false);

        await second.Should().ThrowAsync<SqlException>(
            "the engine must refuse a second active grant even under a different terms version");
    }

    [SkippableFact]
    public async Task RevokedGrant_Should_NotBlockAReGrant()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var guardianId = Guid.NewGuid();

        await InsertGrantAsync(tenantId, subjectId, guardianId, "voice", "v1", revoked: true);

        // Re-consent after withdrawal, or after a terms supersede, must be possible -- otherwise a
        // family could never restore a purpose they once withdrew.
        var reGrant = async () => await InsertGrantAsync(
            tenantId, subjectId, guardianId, "voice", "v2", revoked: false);

        await reGrant.Should().NotThrowAsync();
    }

    private async Task InsertGrantAsync(
        Guid tenantId, Guid subjectId, Guid grantedById, string purpose, string termsVersion, bool revoked)
    {
        await using var connection = new SqlConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AnkConsentGrants
                (Id, TenantId, SubjectPartyId, GrantedByPartyId, Purpose, TermsVersion, Jurisdiction,
                 VerificationMethod, VerifiedAt, GrantedAt, RevokedAt, CreatedAt, IsDeleted)
            VALUES
                (@id, @tenantId, @subjectId, @grantedById, @purpose, @termsVersion, 'GB',
                 'payment-instrument', SYSUTCDATETIME(), SYSUTCDATETIME(), @revokedAt, SYSUTCDATETIME(), 0)
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid());
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@subjectId", subjectId);
        command.Parameters.AddWithValue("@grantedById", grantedById);
        command.Parameters.AddWithValue("@purpose", purpose);
        command.Parameters.AddWithValue("@termsVersion", termsVersion);
        command.Parameters.AddWithValue("@revokedAt", revoked ? DateTime.UtcNow : DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<(bool IsUnique, string Filter)> IndexShapeAsync(string table, string index)
    {
        await using var connection = new SqlConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.is_unique, ISNULL(i.filter_definition, '')
            FROM sys.indexes i
            JOIN sys.tables t ON t.object_id = i.object_id
            WHERE t.name = @table AND i.name = @index
            """;
        command.Parameters.AddWithValue("@table", table);
        command.Parameters.AddWithValue("@index", index);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue($"index {index} must exist on {table}");
        return (reader.GetBoolean(0), reader.GetString(1));
    }

    private async Task<List<string>> IndexColumnsAsync(string table, string index)
    {
        await using var connection = new SqlConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.name
            FROM sys.index_columns ic
            JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            JOIN sys.tables t ON t.object_id = i.object_id
            WHERE t.name = @table AND i.name = @index AND ic.is_included_column = 0
            ORDER BY ic.key_ordinal
            """;
        command.Parameters.AddWithValue("@table", table);
        command.Parameters.AddWithValue("@index", index);
        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
