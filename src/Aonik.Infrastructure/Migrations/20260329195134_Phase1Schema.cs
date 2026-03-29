using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase1Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop old insight index if it exists
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AnkInsights') AND name = 'IX_AnkInsights_SubjectType_SubjectId') " +
                "DROP INDEX IX_AnkInsights_SubjectType_SubjectId ON dbo.AnkInsights;");

            // Extend Insight entity
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AnkInsights') AND name = 'ExpiresAt') " +
                "ALTER TABLE dbo.AnkInsights ADD ExpiresAt datetime2 NULL;");
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AnkInsights') AND name = 'MetadataJson') " +
                "ALTER TABLE dbo.AnkInsights ADD MetadataJson nvarchar(max) NULL;");
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AnkInsights') AND name = 'TenantId') " +
                "ALTER TABLE dbo.AnkInsights ADD TenantId uniqueidentifier NOT NULL CONSTRAINT DF_AnkInsights_TenantId DEFAULT '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AnkInsights') AND name = 'UserId') " +
                "ALTER TABLE dbo.AnkInsights ADD UserId uniqueidentifier NULL;");

            // Add AgentType to agents
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AnkAgents') AND name = 'AgentType') " +
                "ALTER TABLE dbo.AnkAgents ADD AgentType int NOT NULL CONSTRAINT DF_AnkAgents_AgentType DEFAULT 0;");

            // Create ConversationSummaries
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = 'ConversationSummaries')
BEGIN
    CREATE TABLE [dbo].[ConversationSummaries] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ChatThreadId] uniqueidentifier NOT NULL,
        [SessionStartedAt] datetime2 NOT NULL,
        [SessionEndedAt] datetime2 NULL,
        [SummaryText] nvarchar(2000) NOT NULL,
        [KeyDecisionsJson] nvarchar(max) NULL,
        [OpenLoopsJson] nvarchar(max) NULL,
        [RecommendationOutcomesJson] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ConversationSummaries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConversationSummaries_AnkChatThreads_ChatThreadId] FOREIGN KEY ([ChatThreadId])
            REFERENCES [dbo].[AnkChatThreads] ([Id]) ON DELETE CASCADE
    );
END");

            // Create UserMemoryEntry
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = 'UserMemoryEntry')
BEGIN
    CREATE TABLE [dbo].[UserMemoryEntry] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [EntryType] nvarchar(50) NOT NULL,
        [Key] nvarchar(256) NOT NULL,
        [ValueJson] nvarchar(max) NOT NULL,
        [Confidence] decimal(3,2) NOT NULL,
        [Source] nvarchar(50) NOT NULL,
        [AiRunId] uniqueidentifier NULL,
        [SupersededById] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastConfirmedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserMemoryEntry] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserMemoryEntry_AnkAiRuns_AiRunId] FOREIGN KEY ([AiRunId])
            REFERENCES [dbo].[AnkAiRuns] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_UserMemoryEntry_UserMemoryEntry_SupersededById] FOREIGN KEY ([SupersededById])
            REFERENCES [dbo].[UserMemoryEntry] ([Id]) ON DELETE NO ACTION
    );
END");

            // Indexes (idempotent)
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AnkInsights') AND name = 'IX_Insights_Tenant_SubjectType_SubjectId') " +
                "CREATE INDEX IX_Insights_Tenant_SubjectType_SubjectId ON dbo.AnkInsights (TenantId, SubjectType, SubjectId);");
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AnkInsights') AND name = 'IX_Insights_Tenant_UserId') " +
                "CREATE INDEX IX_Insights_Tenant_UserId ON dbo.AnkInsights (TenantId, UserId) WHERE UserId IS NOT NULL;");
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ConversationSummaries') AND name = 'IX_ConversationSummaries_ChatThreadId') " +
                "CREATE UNIQUE INDEX IX_ConversationSummaries_ChatThreadId ON dbo.ConversationSummaries (ChatThreadId);");
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ConversationSummaries') AND name = 'IX_ConversationSummaries_TenantUser_SessionStart') " +
                "CREATE INDEX IX_ConversationSummaries_TenantUser_SessionStart ON dbo.ConversationSummaries (TenantId, UserId, SessionStartedAt DESC);");
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.UserMemoryEntry') AND name = 'IX_UserMemoryEntries_TenantUser_Current') " +
                "CREATE INDEX IX_UserMemoryEntries_TenantUser_Current ON dbo.UserMemoryEntry (TenantId, UserId, SupersededById) WHERE SupersededById IS NULL;");
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.UserMemoryEntry') AND name = 'IX_UserMemoryEntries_TenantUser_EntryType') " +
                "CREATE INDEX IX_UserMemoryEntries_TenantUser_EntryType ON dbo.UserMemoryEntry (TenantId, UserId, EntryType);");
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.UserMemoryEntry') AND name = 'IX_UserMemoryEntries_TenantUser_Key') " +
                "CREATE INDEX IX_UserMemoryEntries_TenantUser_Key ON dbo.UserMemoryEntry (TenantId, UserId, [Key]);");
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.UserMemoryEntry') AND name = 'IX_UserMemoryEntry_AiRunId') " +
                "CREATE INDEX IX_UserMemoryEntry_AiRunId ON dbo.UserMemoryEntry (AiRunId);");
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.UserMemoryEntry') AND name = 'IX_UserMemoryEntry_SupersededById') " +
                "CREATE INDEX IX_UserMemoryEntry_SupersededById ON dbo.UserMemoryEntry (SupersededById);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[ConversationSummaries];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[UserMemoryEntry];");

            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AnkInsights') AND name = 'IX_Insights_Tenant_SubjectType_SubjectId') " +
                "DROP INDEX IX_Insights_Tenant_SubjectType_SubjectId ON dbo.AnkInsights;");
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AnkInsights') AND name = 'IX_Insights_Tenant_UserId') " +
                "DROP INDEX IX_Insights_Tenant_UserId ON dbo.AnkInsights;");

            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AnkInsights') AND name = 'ExpiresAt') " +
                "ALTER TABLE dbo.AnkInsights DROP COLUMN ExpiresAt;");
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AnkInsights') AND name = 'MetadataJson') " +
                "ALTER TABLE dbo.AnkInsights DROP COLUMN MetadataJson;");
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AnkInsights') AND name = 'TenantId') " +
                "ALTER TABLE dbo.AnkInsights DROP CONSTRAINT IF EXISTS DF_AnkInsights_TenantId; " +
                "IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AnkInsights') AND name = 'TenantId') " +
                "ALTER TABLE dbo.AnkInsights DROP COLUMN TenantId;");
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AnkInsights') AND name = 'UserId') " +
                "ALTER TABLE dbo.AnkInsights DROP COLUMN UserId;");
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AnkAgents') AND name = 'AgentType') " +
                "ALTER TABLE dbo.AnkAgents DROP CONSTRAINT IF EXISTS DF_AnkAgents_AgentType; " +
                "IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AnkAgents') AND name = 'AgentType') " +
                "ALTER TABLE dbo.AnkAgents DROP COLUMN AgentType;");

            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AnkInsights') AND name = 'IX_AnkInsights_SubjectType_SubjectId') " +
                "CREATE INDEX IX_AnkInsights_SubjectType_SubjectId ON dbo.AnkInsights (SubjectType, SubjectId);");
        }
    }
}
