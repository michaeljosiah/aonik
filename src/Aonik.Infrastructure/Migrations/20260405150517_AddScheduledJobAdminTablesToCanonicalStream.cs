using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Idempotent: these tables may already exist in dev from the now-deleted
    /// PlatformDbContext migration stream. Each CREATE is guarded with
    /// IF OBJECT_ID so the migration succeeds regardless.
    /// </remarks>
    public partial class AddScheduledJobAdminTablesToCanonicalStream : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── AnkScheduledJobAdminCommands ──────────────────────────────
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[AnkScheduledJobAdminCommands]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[AnkScheduledJobAdminCommands] (
                        [Id]               uniqueidentifier NOT NULL,
                        [TenantId]         uniqueidentifier NOT NULL,
                        [JobName]          nvarchar(200)    NOT NULL,
                        [GroupName]        nvarchar(100)    NOT NULL,
                        [CommandType]      nvarchar(50)     NOT NULL,
                        [PayloadJson]      nvarchar(max)    NOT NULL,
                        [RequestedByUserId] uniqueidentifier NULL,
                        [Status]           nvarchar(50)     NOT NULL,
                        [ResultMessage]    nvarchar(1000)   NULL,
                        [ProcessedAtUtc]   datetime2        NULL,
                        [CreatedAt]        datetime2        NOT NULL,
                        [CreatedBy]        uniqueidentifier NULL,
                        [UpdatedAt]        datetime2        NULL,
                        [UpdatedBy]        uniqueidentifier NULL,
                        [RowVersion]       rowversion       NOT NULL,
                        [IsDeleted]        bit              NOT NULL,
                        [DeletedAt]        datetime2        NULL,
                        [DeletedBy]        uniqueidentifier NULL,
                        CONSTRAINT [PK_AnkScheduledJobAdminCommands] PRIMARY KEY ([Id])
                    );
                END
                """);

            // ── AnkScheduledJobProjections ────────────────────────────────
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[AnkScheduledJobProjections]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[AnkScheduledJobProjections] (
                        [Id]                uniqueidentifier NOT NULL,
                        [TenantId]          uniqueidentifier NOT NULL,
                        [JobName]           nvarchar(200)    NOT NULL,
                        [GroupName]         nvarchar(100)    NOT NULL,
                        [DisplayName]       nvarchar(200)    NOT NULL,
                        [Description]       nvarchar(1000)   NOT NULL,
                        [CronExpression]    nvarchar(120)    NOT NULL,
                        [TimeZoneId]        nvarchar(100)    NOT NULL,
                        [State]             nvarchar(50)     NOT NULL,
                        [NextFireTimeUtc]   datetime2        NULL,
                        [PreviousFireTimeUtc] datetime2      NULL,
                        [LastOutcome]       nvarchar(50)     NULL,
                        [LastOutcomeSummary] nvarchar(1000)  NULL,
                        [LastDurationMs]    int              NULL,
                        [LastSyncedAtUtc]   datetime2        NOT NULL,
                        [CreatedAt]         datetime2        NOT NULL,
                        [CreatedBy]         uniqueidentifier NULL,
                        [UpdatedAt]         datetime2        NULL,
                        [UpdatedBy]         uniqueidentifier NULL,
                        [RowVersion]        rowversion       NOT NULL,
                        [IsDeleted]         bit              NOT NULL,
                        [DeletedAt]         datetime2        NULL,
                        [DeletedBy]         uniqueidentifier NULL,
                        CONSTRAINT [PK_AnkScheduledJobProjections] PRIMARY KEY ([Id])
                    );
                END
                """);

            // ── AnkScheduledJobRuns ───────────────────────────────────────
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[AnkScheduledJobRuns]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[AnkScheduledJobRuns] (
                        [Id]              uniqueidentifier NOT NULL,
                        [TenantId]        uniqueidentifier NOT NULL,
                        [JobName]         nvarchar(200)    NOT NULL,
                        [GroupName]       nvarchar(100)    NOT NULL,
                        [Outcome]         nvarchar(50)     NOT NULL,
                        [ErrorMessage]    nvarchar(2000)   NULL,
                        [DurationMs]      int              NOT NULL,
                        [TriggeredBy]     nvarchar(50)     NOT NULL,
                        [FiredAtUtc]      datetime2        NOT NULL,
                        [CompletedAtUtc]  datetime2        NOT NULL,
                        [FireInstanceId]  nvarchar(200)    NULL,
                        CONSTRAINT [PK_AnkScheduledJobRuns] PRIMARY KEY ([Id])
                    );
                END
                """);

            // ── AnkSchedulerHealthSnapshots ───────────────────────────────
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[AnkSchedulerHealthSnapshots]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[AnkSchedulerHealthSnapshots] (
                        [Id]                  uniqueidentifier NOT NULL,
                        [TenantId]            uniqueidentifier NOT NULL,
                        [SchedulerName]       nvarchar(200)    NOT NULL,
                        [SchedulerInstanceId] nvarchar(200)    NOT NULL,
                        [IsStarted]           bit              NOT NULL,
                        [InStandbyMode]       bit              NOT NULL,
                        [ThreadPoolSize]      int              NOT NULL,
                        [ActiveJobCount]      int              NOT NULL,
                        [TotalJobCount]       int              NOT NULL,
                        [TotalTriggerCount]   int              NOT NULL,
                        [RecordedAtUtc]       datetime2        NOT NULL,
                        CONSTRAINT [PK_AnkSchedulerHealthSnapshots] PRIMARY KEY ([Id])
                    );
                END
                """);

            // ── Indexes (idempotent — guarded with IF NOT EXISTS) ────────
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ScheduledJobAdminCommand_GroupName_JobName_Status')
                    CREATE INDEX [IX_ScheduledJobAdminCommand_GroupName_JobName_Status]
                        ON [dbo].[AnkScheduledJobAdminCommands] ([GroupName], [JobName], [Status]);
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ScheduledJobAdminCommand_Status_CreatedAt')
                    CREATE INDEX [IX_ScheduledJobAdminCommand_Status_CreatedAt]
                        ON [dbo].[AnkScheduledJobAdminCommands] ([Status], [CreatedAt]);
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ScheduledJobProjection_GroupName_JobName')
                    CREATE UNIQUE INDEX [IX_ScheduledJobProjection_GroupName_JobName]
                        ON [dbo].[AnkScheduledJobProjections] ([GroupName], [JobName]);
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ScheduledJobRun_FiredAtUtc')
                    CREATE INDEX [IX_ScheduledJobRun_FiredAtUtc]
                        ON [dbo].[AnkScheduledJobRuns] ([FiredAtUtc]);
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ScheduledJobRun_GroupName_JobName_FiredAtUtc')
                    CREATE INDEX [IX_ScheduledJobRun_GroupName_JobName_FiredAtUtc]
                        ON [dbo].[AnkScheduledJobRuns] ([GroupName], [JobName], [FiredAtUtc]);
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SchedulerHealthSnapshot_Name_InstanceId')
                    CREATE UNIQUE INDEX [IX_SchedulerHealthSnapshot_Name_InstanceId]
                        ON [dbo].[AnkSchedulerHealthSnapshots] ([SchedulerName], [SchedulerInstanceId]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkScheduledJobAdminCommands",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkScheduledJobProjections",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkScheduledJobRuns",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkSchedulerHealthSnapshots",
                schema: "dbo");
        }
    }
}
