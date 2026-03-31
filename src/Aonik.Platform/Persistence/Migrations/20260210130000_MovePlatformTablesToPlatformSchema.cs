using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Aonik.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(PlatformDbContext))]
    [Migration("20260210130000_MovePlatformTablesToPlatformSchema")]
    public partial class MovePlatformTablesToPlatformSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create schema if it doesn't exist
            migrationBuilder.Sql(@"
IF NOT EXISTS(SELECT * FROM sys.schemas WHERE name = 'platform')
    EXEC('CREATE SCHEMA [platform]');
");

            // Create a simple migration run log table in the platform schema to record progress
            migrationBuilder.Sql(@"
IF OBJECT_ID('platform.MigrationRunLogs', 'U') IS NULL
BEGIN
    CREATE TABLE platform.MigrationRunLogs (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        StartedAtUtc DATETIME2 NOT NULL,
        FinishedAtUtc DATETIME2 NULL,
        Status NVARCHAR(50) NOT NULL,
        Details NVARCHAR(MAX) NULL,
        TriggeredBy NVARCHAR(200) NULL
    );
END
");

            // Generate a run id here and record the start. We embed the GUID in the SQL strings below.
            var runId = Guid.NewGuid();
            migrationBuilder.Sql($"INSERT INTO platform.MigrationRunLogs (Id, StartedAtUtc, Status) VALUES ('{runId}', SYSUTCDATETIME(), 'Started')");

            // Tables to move from dbo -> platform (if they exist)
            var tables = new[] {
                "Tenants",
                "TenantCountries",
                "TenantCurrencies",
                "Users",
                "Roles",
                "Permissions",
                "UserRoles",
                "RolePermissions",
                "UserParties",
                "VerificationChallenges",
                "Parties",
                "PartyAddresses",
                "PartyContacts",
                "PartyConsents",
                "PersonProfiles",
                "BusinessProfiles",
                "ExternalAccounts",
                "PartyRoleAssignments",
                "PartyRelationships",
                "ScreeningChecks",
                "ComplianceCases",
                "AuditLogs",
                "Documents",
                "DocumentFiles",
                "DocumentUsages",
                "DocumentVerifications",
                "DocumentVersions",
                "NotificationTemplates",
                "NotificationTemplateBindings",
                "WebhookSubscriptions",
                "WorkItems",
                "Jobs",
                "ContentBlocks",
                "ContentBlockMedia",
                "TenantFeatures",
                "Settings",
                "ReferenceData",
                "Countries",
                "Currencies",
                "CountryCurrencies",
                "AutonumberProfiles",
                "AutonumberReservations"
            };

            // Pre-check: if tables exist in neither dbo nor platform, skip gracefully.
            // On fresh environments, AonikDbContext creates tables with the Ank prefix
            // (e.g. dbo.AnkTenants), so the bare dbo.Tenants won't exist — that's expected.
            // Only attempt the schema transfer when there's actually something to move.
            migrationBuilder.Sql($@"
DECLARE @MissingCount INT = 0;
{string.Join("\n", tables.Select(t => $"IF OBJECT_ID('dbo.{t}', 'U') IS NULL AND OBJECT_ID('platform.{t}', 'U') IS NULL SET @MissingCount = @MissingCount + 1;"))}
IF @MissingCount = {tables.Length}
BEGIN
    UPDATE platform.MigrationRunLogs SET FinishedAtUtc = SYSUTCDATETIME(), Status = 'Skipped', Details = 'No bare-named tables found — fresh environment with prefixed tables' WHERE Id = '{runId}';
    RETURN;
END
");

            // Transfer tables that exist in dbo and not yet in platform
            foreach (var table in tables)
            {
                migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL AND OBJECT_ID('platform.{table}', 'U') IS NULL
BEGIN
    ALTER SCHEMA [platform] TRANSFER [dbo].[{table}];
END
");
            }

            // Mark completed (only if not already skipped by the pre-check)
            migrationBuilder.Sql($"UPDATE platform.MigrationRunLogs SET FinishedAtUtc = SYSUTCDATETIME(), Status = 'Completed' WHERE Id = '{runId}' AND Status = 'Started'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Move tables back from platform -> dbo (if they exist)
            var tables = new[] {
                "AutonumberReservations",
                "AutonumberProfiles",
                "CountryCurrencies",
                "Currencies",
                "Countries",
                "ReferenceData",
                "Settings",
                "TenantFeatures",
                "ContentBlockMedia",
                "ContentBlocks",
                "Jobs",
                "WorkItems",
                "WebhookSubscriptions",
                "NotificationTemplateBindings",
                "NotificationTemplates",
                "DocumentVersions",
                "DocumentVerifications",
                "DocumentUsages",
                "DocumentFiles",
                "Documents",
                "AuditLogs",
                "ComplianceCases",
                "ScreeningChecks",
                "PartyRelationships",
                "PartyRoleAssignments",
                "ExternalAccounts",
                "BusinessProfiles",
                "PersonProfiles",
                "PartyConsents",
                "PartyContacts",
                "PartyAddresses",
                "Parties",
                "VerificationChallenges",
                "UserParties",
                "RolePermissions",
                "UserRoles",
                "Permissions",
                "Roles",
                "Users",
                "TenantCurrencies",
                "TenantCountries",
                "Tenants"
            };

            foreach (var table in tables)
            {
                migrationBuilder.Sql($@"
IF OBJECT_ID('platform.{table}', 'U') IS NOT NULL AND OBJECT_ID('dbo.{table}', 'U') IS NULL
    EXEC('ALTER SCHEMA [dbo] TRANSFER [platform].[{table}]');
");
            }

            // Optionally drop platform schema if empty (safe no-op if not empty)
            migrationBuilder.Sql(@"
IF NOT EXISTS(SELECT * FROM sys.objects o JOIN sys.schemas s ON o.schema_id = s.schema_id WHERE s.name = 'platform')
    EXEC('DROP SCHEMA [platform]');
");
        }
    }
}
