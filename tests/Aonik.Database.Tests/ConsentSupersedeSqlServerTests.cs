using Aonik.IntegrationTests.Support;

using FluentAssertions;

using Microsoft.Data.SqlClient;

namespace Aonik.Database.Tests;

/// <summary>
/// Spec 095 §10.2 — the atomic supersede, against a real engine.
///
/// <para>
/// This lane exists because the InMemory provider enforces neither the filtered unique index nor
/// transactions, so the unit tests for <c>GrantAsync</c> prove the <em>intent</em> and cannot prove
/// the <em>guarantee</em>. Here the index is real and can reject.
/// </para>
///
/// <para>
/// The specific risk: revoking the old grant and inserting the new one happen in one
/// <c>SaveChanges</c>, and for two rows of the same entity type EF Core has no dependency to order
/// them by. If the INSERT is sent before the UPDATE, both rows are momentarily unrevoked and the
/// filtered unique index rejects the batch — a failure that only ever appears against SQL Server.
/// </para>
/// </summary>
public class ConsentSupersedeSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;

    public ConsentSupersedeSqlServerTests(SqlLocalDbFixture db)
    {
        _db = db;
    }

    [SkippableFact]
    public async Task Supersede_Should_Succeed_When_RevokeAndInsertShareOneStatementBatch()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var guardianId = Guid.NewGuid();

        var v1 = await InsertGrantAsync(tenantId, subjectId, guardianId, "service-core", "v1");

        // Exactly the shape ConsentService.GrantAsync produces: revoke the prior version and insert
        // the replacement inside one transaction. If the engine sees the insert first, the filtered
        // unique index rejects it.
        await using var connection = new SqlConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        await using (var revoke = connection.CreateCommand())
        {
            revoke.Transaction = transaction;
            revoke.CommandText = "UPDATE AnkConsentGrants SET RevokedAt = SYSUTCDATETIME() WHERE Id = @id";
            revoke.Parameters.AddWithValue("@id", v1);
            await revoke.ExecuteNonQueryAsync();
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = InsertSql;
            AddGrantParameters(insert, Guid.NewGuid(), tenantId, subjectId, guardianId, "service-core", "v2", revoked: false);
            await insert.ExecuteNonQueryAsync();
        }

        var commit = async () => await transaction.CommitAsync();
        await commit.Should().NotThrowAsync(
            "revoke-then-insert inside one transaction must satisfy the single-active-grant index");

        (await ActiveGrantCountAsync(tenantId, subjectId, "service-core")).Should().Be(1);
    }

    [SkippableFact]
    public async Task Supersede_Should_BeRejected_When_TheInsertLandsBeforeTheRevoke()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var guardianId = Guid.NewGuid();

        await InsertGrantAsync(tenantId, subjectId, guardianId, "service-core", "v1");

        // The failure mode this test pins down: inserting the replacement while the prior grant is
        // still active. The engine refuses, which is the desired behaviour — the index is what stops
        // a mis-ordered implementation from silently leaving two active versions and letting the
        // version-agnostic reader authorise the stale one.
        var insertFirst = async () => await InsertGrantAsync(
            tenantId, subjectId, guardianId, "service-core", "v2");

        await insertFirst.Should().ThrowAsync<SqlException>();
    }

    [SkippableFact]
    public async Task PublishTerms_Should_LeaveNoActiveGrant_ForAnAffectedPurpose()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var guardianId = Guid.NewGuid();

        await InsertGrantAsync(tenantId, subjectId, guardianId, "service-core", "v1");

        // Publication revokes without inserting a replacement, so the subject is left with NO active
        // grant until they re-consent. That is the specified behaviour and the operational cost of
        // a material terms change.
        await using var connection = new SqlConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE AnkConsentGrants
               SET RevokedAt = SYSUTCDATETIME()
             WHERE TenantId = @tenantId AND Purpose = 'service-core'
               AND TermsVersion <> 'v2' AND RevokedAt IS NULL
            """;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        await command.ExecuteNonQueryAsync();

        (await ActiveGrantCountAsync(tenantId, subjectId, "service-core")).Should().Be(0);
    }

    private const string InsertSql = """
        INSERT INTO AnkConsentGrants
            (Id, TenantId, SubjectPartyId, GrantedByPartyId, Purpose, TermsVersion, Jurisdiction,
             VerificationMethod, VerifiedAt, GrantedAt, RevokedAt, CreatedAt, IsDeleted)
        VALUES
            (@id, @tenantId, @subjectId, @grantedById, @purpose, @termsVersion, 'GB',
             'payment-instrument', SYSUTCDATETIME(), SYSUTCDATETIME(), @revokedAt, SYSUTCDATETIME(), 0)
        """;

    private static void AddGrantParameters(
        SqlCommand command, Guid id, Guid tenantId, Guid subjectId, Guid grantedById,
        string purpose, string termsVersion, bool revoked)
    {
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@subjectId", subjectId);
        command.Parameters.AddWithValue("@grantedById", grantedById);
        command.Parameters.AddWithValue("@purpose", purpose);
        command.Parameters.AddWithValue("@termsVersion", termsVersion);
        command.Parameters.AddWithValue("@revokedAt", revoked ? DateTime.UtcNow : DBNull.Value);
    }

    private async Task<Guid> InsertGrantAsync(
        Guid tenantId, Guid subjectId, Guid grantedById, string purpose, string termsVersion)
    {
        var id = Guid.NewGuid();
        await using var connection = new SqlConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = InsertSql;
        AddGrantParameters(command, id, tenantId, subjectId, grantedById, purpose, termsVersion, revoked: false);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private async Task<int> ActiveGrantCountAsync(Guid tenantId, Guid subjectId, string purpose)
    {
        await using var connection = new SqlConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM AnkConsentGrants
             WHERE TenantId = @tenantId AND SubjectPartyId = @subjectId
               AND Purpose = @purpose AND RevokedAt IS NULL
            """;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@subjectId", subjectId);
        command.Parameters.AddWithValue("@purpose", purpose);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
